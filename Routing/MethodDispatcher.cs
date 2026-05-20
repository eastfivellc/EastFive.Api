using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

using EastFive.Api.Core;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;

namespace EastFive.Api.Routing
{
    /// <summary>
    /// Stateless dispatch pipeline for the current (template-based) routing
    /// path. Composed of pure functions; the only mutable state is the
    /// per-request bindings list inside <see cref="BindAndInvokeAsync"/>.
    ///
    /// Pipeline:
    ///   1. <see cref="PickDeserializerAsync"/> — pick highest-priority
    ///      <see cref="IDeserializeRequestEnvelope"/>, build the envelope.
    ///   2. <see cref="BuildMatches"/> — per <see cref="RouteCandidate"/>,
    ///      ask each parameter's binding-requirement provider whether the
    ///      envelope can fulfil it.
    ///   3. <see cref="DispatchAsync"/> — pick the single valid match,
    ///      wrap with <see cref="IHandleRoutes"/>, hand off to bind+invoke.
    ///   4. <see cref="BindAndInvokeAsync"/> — extract each parameter's
    ///      value via its <see cref="ParameterFulfillment"/>, then dispatch
    ///      through the <see cref="IHandleMethodInvocation"/> chain.
    ///   5. <see cref="RunInvocationChainAsync"/> — compose the application,
    ///      method, and per-parameter handlers and invoke the controller.
    /// </summary>
    public static class MethodDispatcher
    {
        private static readonly IReadOnlyDictionary<ParameterInfo, object> EmptyBindingContexts =
            new Dictionary<ParameterInfo, object>(0);

        /// <summary>
        /// Pass 1: pick the highest-priority <see cref="IDeserializeRequestEnvelope"/>
        /// that <see cref="IDeserializeRequestEnvelope.CanClassify"/>s the request,
        /// and build its envelope (Pass 2). Returns either an envelope or an
        /// error response if no deserializer claimed the request.
        /// </summary>
        public static async Task<IHttpResponse>
            PickDeserializerAsync(IApplication httpApp, IHttpRequest request,
                Func<IRequestEnvelope, Task<IHttpResponse>> onContinueAsync)
        {
            var classified = httpApp.GetType()
                .GetAttributesInterface<IDeserializeRequestEnvelope>(true, true)
                .Where(d => d.CanClassify(request))
                .OrderByDescending(d => d.Priority)
                .ToArray();

            return await classified
                .Single(
                    onNone: () =>
                    {
                        return request
                            .CreateResponse(HttpStatusCode.NotImplemented)
                            .AddReason($"No IDeserializeRequestEnvelope claimed the request")
                            .AsTask();
                    },
                    onSingle: async deserializer =>
                    {
                        var envelope = await deserializer.CreateEnvelopeAsync(request, httpApp);
                        return await onContinueAsync(envelope);
                    },
                    onMultiple: async deserializers =>
                    {
                        var deserializer = deserializers[0];
                        if (classified.Length > 1
                            && Math.Abs(classified[1].Priority - deserializer.Priority) < double.Epsilon)
                        {
                            var requestVerb = request.Method?.Method ?? string.Empty;
                            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                            var tied = classified
                                .TakeWhile(d => Math.Abs(d.Priority - deserializer.Priority) < double.Epsilon)
                                .Select(d => d.GetType().Name)
                                .Join(", ");
                            System.Diagnostics.Trace.TraceWarning(
                                $"IDeserializeRequestEnvelope tie at priority {deserializer.Priority} for {requestVerb} {path}: {tied}; picking {deserializer.GetType().Name}");
                        }
                        var envelope = await deserializer.CreateEnvelopeAsync(request, httpApp);
                        return await onContinueAsync(envelope);
                    });
        }

        /// <summary>
        /// Pass 3a: build a <see cref="MethodMatch"/> per route candidate by
        /// asking each candidate's <see cref="RouteEnvelope"/> to fulfil the
        /// method's binding requirements. The envelope is shared across
        /// candidates (one per request); captures are per-candidate.
        /// </summary>
        public static MethodMatch[] BuildMatches(IRequestEnvelope envelope,
            RouteCandidate[] routeMatches)
        {
            return routeMatches
                .Select(rc =>
                {
                    var method = rc.Method.IsGenericMethod
                        ? rc.Method.MakeGenericMethod(rc.ControllerType.AsArray())
                        : rc.Method;
                    var routeEnvelope = new RouteEnvelope(envelope, rc.Captures);
                    var fulfillments = method
                        .GetParameters()
                        .Select(p =>
                            {
                                if (p.TryGetAttributeInterface<IProvideBindingRequirements>(out var provider))
                                {
                                    var (requirements, assemble) = provider.GetParameterBinding(p);
                                    return (ok: true, pf: ParameterFulfillment.From(p, requirements, assemble, routeEnvelope));
                                }
                                return (ok: false, pf: default(ParameterFulfillment));
                            })
                        .Where(t => t.ok)
                        .Select(t => t.pf)
                        .ToArray();
                    return new MethodMatch(rc.ControllerType, rc.InvokeResource,
                        method, fulfillments);
                })
                .ToArray();
        }

        /// <summary>
        /// Final dispatch step: filter <paramref name="matches"/> to valid ones,
        /// fail with 501 (none) / 500 (multiple), or wrap the single chosen
        /// match in <see cref="IHandleRoutes"/> and bind+invoke.
        /// </summary>
        public static Task<IHttpResponse> DispatchAsync(IApplication httpApp,
            IHttpRequest request, MethodMatch[] matches)
        {
            return matches
                .Where(m => m.IsValid)
                .Single(
                    onNone: () =>
                    {
                        var reasons = matches
                            .Select(m => $"{m.Method.Name}: {m.ErrorMessage}")
                            .Join("; ");
                        return request
                            .CreateResponse(HttpStatusCode.NotImplemented)
                            .AddReason(reasons)
                            .AsTask();
                    },
                    onSingle: chosen => InvokeChosenAsync(httpApp, request, chosen),
                    onMultiple: validMatches =>
                    {
                        var names = validMatches
                            .Select(m => $"{m.Method.DeclaringType.Name}.{m.Method.Name}")
                            .Join(", ");
                        return request
                            .CreateResponse(HttpStatusCode.InternalServerError)
                            .AddReason($"Ambiguous method match: {names}")
                            .AsTask();
                    });
        }

        private static Task<IHttpResponse> InvokeChosenAsync(IApplication httpApp,
            IHttpRequest request, MethodMatch chosen)
        {
            return httpApp.GetType()
                .GetAttributesInterface<IHandleRoutes>(true, true)
                .Aggregate<IHandleRoutes, RouteHandlingDelegate>(
                    (controllerTypeFinal, httpAppFinal, requestFinal) =>
                        BindAndInvokeAsync(httpAppFinal, requestFinal, chosen),
                    (callback, routeHandler) =>
                    {
                        return (controllerTypeCurrent, httpAppCurrent, requestCurrent) =>
                            routeHandler.HandleRouteAsync(controllerTypeCurrent, chosen.InvokeResource,
                                httpAppCurrent, requestCurrent, callback);
                    })
                .Invoke(chosen.ControllerType, httpApp, request);
        }

        /// <summary>
        /// Pass 3b/3c: bind each parameter via its <see cref="ParameterFulfillment"/>,
        /// then dispatch through <see cref="IHandleMethodInvocation"/>. Bound
        /// values flow as <c>(parameter, value)</c> pairs; per-parameter binding
        /// contexts (e.g. preloaded entities for validators) flow alongside.
        /// </summary>
        public static async Task<IHttpResponse> BindAndInvokeAsync(IApplication httpApp,
            IHttpRequest request, MethodMatch chosen)
        {
            var bindings = new List<KeyValuePair<ParameterInfo, object>>(chosen.Fulfillments.Length);
            var bindingContexts = new Dictionary<ParameterInfo, object>(chosen.Fulfillments.Length);
            foreach (var fulfillment in chosen.Fulfillments)
            {
                var parameter = fulfillment.Parameter;
                var path = fulfillment.Path;
                IHttpResponse failure = null;
                var ok = await fulfillment.ExtractAsync<bool>(httpApp, request,
                    onParsed: (v, ctx) =>
                    {
                        bindings.Add(new KeyValuePair<ParameterInfo, object>(parameter, v));
                        if (ctx != null)
                            bindingContexts[parameter] = ctx;
                        return true;
                    },
                    onFailure: error =>
                    {
                        failure = request
                            .CreateResponse(HttpStatusCode.BadRequest)
                            .AddReason($"could not bind {path}: {error}");
                        return false;
                    });
                if (!ok)
                    return failure;
            }

            return await RunInvocationChainAsync(httpApp, request,
                chosen.ControllerType, chosen.Method,
                bindings.ToArray(), bindingContexts);
        }

        /// <summary>
        /// Compose every <see cref="IHandleMethodInvocation"/> attached to
        /// the application class, the controller method, and each bound
        /// parameter's type into a single sequential chain whose terminal
        /// invokes the controller method.
        ///
        /// Discovery order = run order (outermost first, innermost wraps the
        /// invocation):
        ///   1. Application-class handlers
        ///   2. Controller-method handlers
        ///   3. Per-parameter type handlers (in parameter order)
        ///
        /// The built-in <see cref="BoundParameterValidationHandlerAttribute"/>
        /// applied to <see cref="HttpApplication"/> is just one handler in
        /// this chain — it owns the parameter-bound validator pipeline so
        /// orchestration here stays a single composition.
        /// </summary>
        public static Task<IHttpResponse> RunInvocationChainAsync(
            IApplication httpApp, IHttpRequest request,
            Type controllerType, MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] parameters,
            IReadOnlyDictionary<ParameterInfo, object> bindingContexts)
        {
            var handlers = new List<IHandleMethodInvocation>();
            handlers.AddRange(httpApp.GetType()
                .GetAttributesInterface<IHandleMethodInvocation>(true, true));
            handlers.AddRange(method
                .GetAttributesInterface<IHandleMethodInvocation>(true, true));
            foreach (var binding in parameters)
            {
                if (binding.Key == null)
                    continue;
                handlers.AddRange(binding.Key.ParameterType
                    .GetAttributesInterface<IHandleMethodInvocation>(true, true));
            }

            InvokeMethodDelegate chain =
                (parmsFinal, ctxFinal, methodFinal, appFinal, reqFinal) =>
                    FunctionViewControllerAttribute.InvokeHandledMethodAsync(appFinal, reqFinal,
                        controllerType, methodFinal, parmsFinal);
            for (var i = handlers.Count - 1; i >= 0; i--)
            {
                var h = handlers[i];
                var capturedNext = chain;
                chain = (parmsCur, ctxCur, methodCur, appCur, reqCur) =>
                    h.HandleMethodInvocationAsync(parmsCur, ctxCur, methodCur,
                        appCur, reqCur, capturedNext);
            }
            return chain(parameters, bindingContexts, method, httpApp, request);
        }
    }
}
