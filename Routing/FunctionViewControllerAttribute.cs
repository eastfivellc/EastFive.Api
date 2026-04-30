using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive.Api.Bindings;
using EastFive.Api.Core;
using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using System.IO;
using Newtonsoft.Json;
using EastFive.Api.Bindings.ContentHandlers;

namespace EastFive.Api
{
    public class FunctionViewControllerAttribute 
        : Attribute, IInvokeResource, IDocumentRoute, IProvideSerialization
    {
        private string ns;
        public string Namespace
        {
            get
            {
                if (ns.HasBlackSpace())
                    return ns;
                return "api";
            }
            set
            {
                ns = value;
            }
        }

        public string ExcludeNamespaces { get; set; }
        public string Route { get; set; }

        private string contentType;
        public string ContentType
        {
            get
            {
                if (contentType.HasBlackSpace())
                    return contentType;
                var routeHyphenCase = this.Route.ToHypenCase();
                var routeRenamed = $"x-application/{routeHyphenCase}";
                return routeRenamed;
            }
            set
            {
                this.contentType = value;
            }
        }

        private const double defaultPreference = -111;

        public double Preference { get; set; } = defaultPreference;

        public double GetPreference(IHttpRequest request)
        {
            if (Preference != defaultPreference)
                return Preference;

            return 0.0;
        }

        public string ContentTypeVersion { get; set; }
        public string [] ContentTypeEncodings { get; set; }

        public Uri GetRelativePath(Type resourceDecorated)
        {
            var routeDirectory = this.Namespace.HasBlackSpace() ?
                this.Namespace
                :
                Web.Configuration.Settings.GetString(
                        AppSettings.DefaultNamespace,
                    (ns) => ns,
                    (whyUnspecifiedOrInvalid) => "api");

            var route = this.Route.HasBlackSpace() ?
                this.Route
                :
                resourceDecorated.Name;

            return new Uri($"{routeDirectory}/{route}", UriKind.Relative);
        }

        protected static IDictionary<Type, HttpMethod> methodLookup =
            new Dictionary<Type, HttpMethod>()
            {
                { typeof(EastFive.Api.HttpGetAttribute), HttpMethod.Get },
                { typeof(EastFive.Api.HttpDeleteAttribute), HttpMethod.Delete },
                { typeof(EastFive.Api.HttpPostAttribute), HttpMethod.Post },
                { typeof(EastFive.Api.HttpPutAttribute), HttpMethod.Put },
                { typeof(EastFive.Api.HttpPatchAttribute), new HttpMethod("Patch") },
                { typeof(EastFive.Api.HttpOptionsAttribute), HttpMethod.Options },
                { typeof(EastFive.Api.HttpActionAttribute), new HttpMethod("actions") },
            };

        protected virtual IDictionary<HttpMethod, MethodInfo[]> PossibleHttpMethods(Type controllerType, IApplication httpApp)
        {
            var actionMethods = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(method => method.ContainsCustomAttribute<HttpActionAttribute>())
                .GroupBy(method => method.GetCustomAttribute<HttpActionAttribute>().Method)
                .Select(methodGrp => (new HttpMethod(methodGrp.Key)).PairWithValue(methodGrp.ToArray()));

             return methodLookup
                .Select(
                    methodKvp => methodKvp.Value.PairWithValue(
                        controllerType
                            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                            .Where(method => method.ContainsCustomAttribute(methodKvp.Key))
                            .ToArray()))
                .Concat(actionMethods)
                .ToDictionary();
        }

        public virtual async Task<IHttpResponse> CreateResponseAsync(Type controllerType,
            IApplication httpApp, IHttpRequest request, string[] componentsMatched)
        {
            // Back-compat single-controller façade: middleware drives the
            // method-based pipeline directly now and never calls this. Kept
            // because IInvokeResource still exposes the contract. Filters the
            // shared RouteTable down to entries owned by controllerType so
            // we honour the historical "this controller only" contract.
            var candidates = RouteTable.For(httpApp).Match(request)
                .Where(c => c.ControllerType == controllerType)
                .ToArray();
            if (candidates.Length == 0)
            {
                var requestVerb = request.Method?.Method ?? string.Empty;
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                return request
                    .CreateResponse(HttpStatusCode.NotFound)
                    .AddReason($"No route template matched {requestVerb} {path}");
            }
            var (envelope, deserializerError) = await PickDeserializerAsync(httpApp, request);
            if (envelope == null)
                return deserializerError;
            var matches = BuildMethodMatches(envelope, candidates);
            return await DispatchSelectedAsync(httpApp, request, matches);
        }

        /// <summary>
        /// Pass 1: pick the highest-priority <see cref="IDeserializeRequestEnvelope"/>
        /// that <see cref="IDeserializeRequestEnvelope.CanClassify"/>s the request,
        /// and build its envelope (Pass 2). Returns either an envelope or an
        /// error response if no deserializer claimed the request.
        /// </summary>
        internal static async Task<(IRequestEnvelope envelope, IHttpResponse error)>
            PickDeserializerAsync(IApplication httpApp, IHttpRequest request)
        {
            var classified = httpApp.GetType()
                .GetAttributesInterface<IDeserializeRequestEnvelope>(true, true)
                .Where(d => d.CanClassify(request))
                .OrderByDescending(d => d.Priority)
                .ToArray();

            if (classified.Length == 0)
            {
                return (null, request
                    .CreateResponse(HttpStatusCode.NotImplemented)
                    .AddReason($"No IDeserializeRequestEnvelope claimed the request"));
            }

            var deserializer = classified[0];

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
            return (envelope, null);
        }

        /// <summary>
        /// Pass 3a: build a <see cref="V3MethodMatch"/> per route candidate by
        /// asking each candidate's <see cref="RouteEnvelope"/> to fulfil the
        /// method's binding requirements. The envelope is shared across
        /// candidates (one per request); captures are per-candidate.
        /// </summary>
        internal static V3MethodMatch[] BuildMethodMatches(IRequestEnvelope envelope,
            V3RouteCandidate[] routeMatches)
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
                        .TrySelectWith<ParameterInfo, IProvideBindingRequirement>(
                            (ParameterInfo p, out IProvideBindingRequirement provider) =>
                                p.TryGetAttributeInterface(out provider))
                        .Select(t => t.Item2.GetRequirement(t.Item1))
                        .Select(req => Fulfillment.From(req, routeEnvelope))
                        .ToArray();
                    return new V3MethodMatch(rc.ControllerType, rc.InvokeResource,
                        method, fulfillments);
                })
                .ToArray();
        }

        /// <summary>
        /// Final dispatch step: filter <paramref name="matches"/> to valid ones,
        /// fail with 501 (none) / 500 (multiple), or wrap the single chosen
        /// match in <see cref="IHandleRoutes"/> and bind+invoke.
        /// </summary>
        internal static Task<IHttpResponse> DispatchSelectedAsync(IApplication httpApp,
            IHttpRequest request, V3MethodMatch[] matches)
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
            IHttpRequest request, V3MethodMatch chosen)
        {
            // IHandleRoutes wrapping happens here, around bind+invoke for the
            // chosen method, using the controller it actually came from.
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

        private static async Task<IHttpResponse> BindAndInvokeAsync(IApplication httpApp,
            IHttpRequest request, V3MethodMatch chosen)
        {
            // ----- Pass 3b: bind ------------------------------------------
            var bindings = new List<KeyValuePair<ParameterInfo, object>>(chosen.Fulfillments.Length);
            foreach (var fulfillment in chosen.Fulfillments)
            {
                var requirement = fulfillment.Requirement;
                IHttpResponse failure = null;
                var ok = await fulfillment.ExtractAsync<bool>(httpApp, request,
                    onParsed: v =>
                    {
                        bindings.Add(new KeyValuePair<ParameterInfo, object>(requirement.Parameter, v));
                        return true;
                    },
                    onFailure: error =>
                    {
                        failure = request
                            .CreateResponse(HttpStatusCode.BadRequest)
                            .AddReason($"could not bind {requirement.Path}({requirement.Source}): {error}");
                        return false;
                    });
                if (!ok)
                    return failure;
            }

            return await InvokeValidatedMethodAsync(httpApp, request,
                chosen.ControllerType, chosen.Method, bindings.ToArray());
        }

        internal readonly struct V3RouteCandidate
        {
            public V3RouteCandidate(Type controllerType, IInvokeResource invokeResource,
                MethodInfo method, RouteTemplate template,
                IReadOnlyDictionary<string, string> captures)
            {
                this.ControllerType = controllerType;
                this.InvokeResource = invokeResource;
                this.Method = method;
                this.Template = template;
                this.Captures = captures;
            }

            public Type ControllerType { get; }
            public IInvokeResource InvokeResource { get; }
            public MethodInfo Method { get; }
            public RouteTemplate Template { get; }
            public IReadOnlyDictionary<string, string> Captures { get; }
        }

        internal readonly struct V3MethodMatch
        {
            public V3MethodMatch(Type controllerType, IInvokeResource invokeResource,
                MethodInfo method, Fulfillment[] fulfillments)
            {
                this.ControllerType = controllerType;
                this.InvokeResource = invokeResource;
                this.Method = method;
                this.Fulfillments = fulfillments ?? Array.Empty<Fulfillment>();
            }

            public Type ControllerType { get; }
            public IInvokeResource InvokeResource { get; }
            public MethodInfo Method { get; }
            public Fulfillment[] Fulfillments { get; }

            public bool IsValid => this.Fulfillments.All(f => f.IsValid);

            public string ErrorMessage
                => string.Join(", ", this.Fulfillments
                    .Where(f => !f.IsValid)
                    .Select(f => $"{f.Requirement.Path}({f.Requirement.Source}):envelope could not fulfill"));
        }

        #region Invoke correct method

        protected virtual IEnumerable<MethodInfo> GetHttpMethods(Type controllerType,
            IApplication httpApp, IHttpRequest request, string [] componentsMatched)
        {
            var matchingActionMethods = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(httpApp.GetExtensionMethods(controllerType))
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .Where(
                    method =>
                    {
                        var isMatch = method
                            .GetAttributesInterface<IMatchRoute>()
                            .Any(routeMatcher => routeMatcher.IsMethodMatch(
                                method, request, httpApp, componentsMatched));
                        return isMatch;
                    });
            return matchingActionMethods;
        }

        protected virtual async Task<IHttpResponse> InvokeMethod(
            Type controllerType, IEnumerable<MethodInfo> matchingActionMethods,
            string[] componentsMatched,
            IApplication httpApp, IHttpRequest routeData,
            CastDelegate bodyCastDelegate, string[] bodyValues)
        {
            var evaluatedMethods = matchingActionMethods
                .Select(
                    method =>
                    {
                        var routeMatcher = method.GetAttributesInterface<IMatchRoute>().Single();
                        return routeMatcher.IsRouteMatch(controllerType, method, componentsMatched, this, routeData, httpApp,
                            bodyValues, bodyCastDelegate);
                    });

            var validMethods = evaluatedMethods
                .Where(methodCast => methodCast.isValid);

            return await validMethods
                .First(
                    (methodCast, next) =>
                    {
                        return methodCast.parametersWithValues
                            .Aggregate<SelectParameterResult, ValidateHttpDelegate>(
                                (parameterSelectionUnvalidated, methodFinalUnvalidated, httpAppFinalUnvalidated, requestFinalUnvalidated) =>
                                {
                                    return methodFinalUnvalidated
                                        .GetAttributesInterface<IValidateHttpRequest>(true, true)
                                        .Aggregate<IValidateHttpRequest, ValidateHttpDelegate>(
                                            (parameterSelection, methodFinal, httpAppFinal, routeDataFinal) =>
                                            {
                                                return InvokeValidatedMethodAsync(
                                                    httpAppFinal, routeDataFinal,
                                                    controllerType, methodFinal,
                                                    parameterSelection);
                                            },
                                            (callback, validator) =>
                                            {
                                                return (parametersSelected, methodCurrent, httpAppCurrent, requestCurrent) =>
                                                {
                                                    return validator.ValidateRequest(parametersSelected,
                                                        methodCurrent,
                                                        httpAppCurrent, requestCurrent,
                                                        callback);
                                                };
                                            })
                                        .Invoke(
                                            parameterSelectionUnvalidated,
                                            methodFinalUnvalidated, httpAppFinalUnvalidated, requestFinalUnvalidated);
                                },
                                (callback, parameterSelection) =>
                                {
                                    ValidateHttpDelegate boundCallback =
                                        (parametersSelected, methodCurrent, httpAppCurrent, requestCurrent) =>
                                        {
                                            var paramKvp = parameterSelection.parameterInfo
                                                .PairWithValue(parameterSelection.value);
                                            var updatedParameters = parametersSelected
                                                .Append(paramKvp)
                                                .ToArray();
                                            return callback(updatedParameters, methodCurrent, httpAppCurrent, requestCurrent);
                                        };
                                    var validators = parameterSelection.parameterInfo.ParameterType
                                        .GetAttributesInterface<IValidateHttpRequest>(true, true);
                                    if (!validators.Any())
                                        return boundCallback;
                                    var validator = validators.First();
                                    return (parametersSelected, methodCurrent, httpAppCurrent, requestCurrent) =>
                                    {
                                        return validator.ValidateRequest(parametersSelected,
                                            methodCurrent,
                                            httpAppCurrent, requestCurrent,
                                            boundCallback);
                                    };
                                })
                            .Invoke(
                                new KeyValuePair<ParameterInfo, object>[] { },
                                methodCast.method, httpApp, routeData);
                    },
                    () =>
                    {
                        return Issues(evaluatedMethods).AsTask();
                    });

            IHttpResponse Issues(IEnumerable<RouteMatch> methodsCasts)
            {
                var reasonStrings = methodsCasts
                    .Select(
                        methodCast =>
                        {
                            var errorMessage = methodCast.ErrorMessage;
                            return errorMessage;
                        })
                    .ToArray();
                if (!reasonStrings.Any())
                {
                    return routeData
                        .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                        .AddReason("No methods that implement Action");
                }
                var content = reasonStrings.Join(";");
                return routeData
                    .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                    .AddReason(content);
            }
        }

        internal static Task<IHttpResponse> InvokeValidatedMethodAsync(
            IApplication httpApp, IHttpRequest routeData,
            Type controllerType, MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            return httpApp.GetType()
                .GetAttributesInterface<IHandleMethods>(true, true)
                .Aggregate<IHandleMethods, MethodHandlingDelegate>(
                    (methodFinal, queryParametersFinal, httpAppFinal, routeDataFinal) =>
                    {
                        var response = InvokeHandledMethodAsync(httpApp, routeDataFinal, controllerType, method, queryParameters);
                        return response;
                    },
                    (callback, methodHandler) =>
                    {
                        return (methodCurrent, queryParametersCurrent, httpAppCurrent, requestCurrent) =>
                            methodHandler.HandleMethodAsync(methodCurrent,
                                queryParametersCurrent, httpAppCurrent, requestCurrent,
                                callback);
                    })
                .Invoke(method, queryParameters, httpApp, routeData);
        }

        protected static Task<IHttpResponse> InvokeHandledMethodAsync(
            IApplication httpApp, IHttpRequest routeData,
            Type controllerType, MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            var queryParameterOptions = queryParameters.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
            return method.GetParameters()
                .SelectReduce(
                    async (ParameterInfo methodParameter, Func<object, Task<IHttpResponse>> next) =>
                    {
                        if (queryParameterOptions.ContainsKey(methodParameter.Name))
                            return await next(queryParameterOptions[methodParameter.Name]);

                        return await httpApp.Instigate(routeData, methodParameter, next);
                    },
                    async (object[] methodParameters) =>
                    {
                        try
                        {
                            if (method.IsGenericMethodDefinition)
                            {
                                method = method.MakeGenericMethod(controllerType.AsArray());
                                // var genericArguments = method.GetGenericArguments().Select(arg => arg.Name).Join(",");
                                //return routeData.CreateResponse(HttpStatusCode.InternalServerError)
                                //    .AddReason($"Could not invoke {method.DeclaringType.FullName}..{method.Name} because it contains generic arguments:{genericArguments}");
                            }

                            var response = method.Invoke(null, methodParameters);
                            if (typeof(Api.IHttpResponse).IsAssignableFrom(method.ReturnType))
                                return ((Api.IHttpResponse)response);
                            if (typeof(Task<Api.IHttpResponse>).IsAssignableFrom(method.ReturnType))
                                return (await (Task<Api.IHttpResponse>)response);
                            if (typeof(Task<Task<Api.IHttpResponse>>).IsAssignableFrom(method.ReturnType))
                                return (await await (Task<Task<Api.IHttpResponse>>)response);

                            return (routeData.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
                                .AddReason($"Could not convert type: {method.ReturnType.FullName} to HttpResponseMessage."));
                        }
                        catch (TargetInvocationException ex)
                        {
                            var paramList = methodParameters
                                .Where(p => p != null)
                                .Select(p => p.GetType().FullName).Join(",");
                            var body = ex.InnerException.IsDefaultOrNull() ?
                                ex.StackTrace
                                :
                                $"[{ex.InnerException.GetType().FullName}]{ex.InnerException.Message}:\n{ex.InnerException.StackTrace}";
                            return routeData
                                .CreateResponse(HttpStatusCode.InternalServerError, body)
                                .AddReason($"Could not invoke {method.DeclaringType.FullName}.{method.Name}({paramList})");
                        }
                        catch (Exception ex)
                        {
                            return await httpApp.GetType()
                                .GetAttributesInterface<IHandleExceptions>(true, true)
                                .Aggregate(
                                    (Exception exFinal, MethodInfo methodFinal, KeyValuePair<ParameterInfo, object>[] queryParametersFinal, IApplication httpAppFinal, IHttpRequest routeDataFinal) =>
                                    {
                                        if (ex is IHttpResponseMessageException)
                                        {
                                            var httpResponseMessageException = ex as IHttpResponseMessageException;
                                            return httpResponseMessageException.CreateResponseAsync(
                                                httpApp, routeDataFinal, queryParameterOptions,
                                                method, methodParameters).AsTask<Api.IHttpResponse>();
                                        }

                                        return routeData
                                            .CreateResponse(HttpStatusCode.InternalServerError)
                                            .AddReason(ex.Message)
                                            .AsTask<Api.IHttpResponse>();
                                    },
                                    (HandleExceptionDelegate callback, IHandleExceptions methodHandler) =>
                                    {
                                        return (Exception exCurrent, MethodInfo methodCurrent, KeyValuePair<ParameterInfo, object>[] queryParametersCurrent, IApplication httpAppCurrent, IHttpRequest requestCurrent) =>
                                            methodHandler.HandleExceptionAsync(exCurrent, methodCurrent,
                                            queryParametersCurrent, httpAppCurrent, requestCurrent,
                                            callback);
                                    })
                                .Invoke(ex, method, queryParameters, httpApp, routeData);
                        }

                    });
        }

        #endregion

        public virtual Route GetRoute(Type type, HttpApplication httpApp)
        {
            var actionMethods = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(httpApp.GetExtensionMethods(type))
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .ToArray();

            var ns = this.Namespace.HasBlackSpace() ? this.Namespace : "api";
            return new Route(type, ns, this.Route,
                actionMethods,
                type.GetMembers(BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance),
                httpApp);
        }

        public virtual bool DoesHandleRequest(Type type, IHttpRequest request,
            out double matchQuality, out string [] componentsMatched)
        {
            matchQuality = 
                (this.Route.HasBlackSpace() ? 0 : 2) +
                (this.Namespace.HasBlackSpace() ? 0 : 1);

            var requestUrl = request.GetAbsoluteUri();
            var path = requestUrl.AbsolutePath;
            while (path.Contains("//"))
                path = path.Replace("//", "/");
            var pathParameters = path
                .Split('/'.AsArray())
                .Where(v => v.HasBlackSpace())
                .ToArray();

            if (IsExcluded())
            {
                componentsMatched = new string[] { };
                return false;
            }

            if (!IsNamespaceCorrect(out string [] nsComponents))
            {
                componentsMatched = new string[] { };
                return false;
            }

            if (this.Route.HasBlackSpace())
            {
                var doesMatch = DoesMatch(nsComponents.Length, this.Route, out string [] routeComponents);
                componentsMatched = nsComponents
                    .Concat(routeComponents.NullToEmpty())
                    .ToArray();
                return doesMatch;
            }

            {
                //var route = pathParameters
                //    .Skip(nsComponents.Length)
                //    .First();
                componentsMatched = nsComponents;
                        //.Append(route)
                        //.ToArray();
                return true;
            }

            bool DoesMatch(int index, string value, out string [] matchComponents)
            {
                var valueComponents = value.Split('/');

                if (pathParameters.Length < index + valueComponents.Length)
                {
                    matchComponents = default; // new string[] { };
                    return false;
                }
                matchComponents = pathParameters.Skip(index).Take(valueComponents.Length).ToArray();
                
                if (!valueComponents.SequenceEqual(matchComponents, StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            }

            bool IsNamespaceCorrect(out string [] nsComponents)
            {
                if (this.Namespace.IsNullOrWhiteSpace())
                {
                    nsComponents = pathParameters.Take(1).ToArray();
                    return true;
                }

                if (!Namespace.Contains(','))
                {
                    return DoesMatch(0, this.Namespace, out nsComponents);
                }

                bool doesAnyNamespaceMatch;
                (doesAnyNamespaceMatch, nsComponents) = Namespace
                    .Split(',')
                    .First(
                        (ns, next) =>
                        {
                            if (!DoesMatch(0, ns, out string[] nsInner))
                                return next();
                            return (true, nsInner);
                        },
                        () => (false, new string[] { }));
                return doesAnyNamespaceMatch;
            }

            bool IsExcluded()
            {
                if (this.ExcludeNamespaces.IsNullOrWhiteSpace())
                    return false;
                return this.ExcludeNamespaces
                    .Split(',')
                    .Select(exNs => exNs.ToLower())
                    .First(
                        (exNs, next) =>
                        {
                            if (DoesMatch(0, exNs, out string [] discard))
                                return true;
                            return next();
                        },
                        () => false);
            }
        }

        #region Serialization

        public string MediaType => "application/json";

        public Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeConvert(request, httpApp);
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                });
            var contentType = this.ContentType.HasBlackSpace() ?
                this.ContentType
                :
                this.MediaType;
            return responseStream.WriteResponseText(jsonObj, request);
            //using (var streamWriter = request.TryGetAcceptEncoding(out Encoding writerEncoding) ?
            //    new StreamWriter(responseStream, writerEncoding)
            //    :
            //    new StreamWriter(responseStream, Encoding.UTF8))
            //{
            //    await streamWriter.WriteAsync(jsonObj);
            //    await streamWriter.FlushAsync();
            //}
        }

        #endregion

        #region Collection parameters

        public delegate TResult ParseContentDelegate<TResult>(Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);

        private struct MultipartParameter
        {
            public string index;
            public string key;
            public Func<Type, Func<object, object>, Func<string, object>, object> fetchValue;
        }

        protected struct MethodCast
        {
            public bool valid;
            public string[] extraBodyParams;
            public string[] extraQueryParams;
            public SelectParameterResult[] failedValidations;
            public MethodInfo method;

            public string ErrorMessage
            {
                get
                {
                    var failedValidationErrorMessages = failedValidations
                        .Select(
                            paramResult =>
                            {
                                var validator = paramResult.parameterInfo.GetAttributeInterface<IBindApiValue>();
                                var lookupName = validator.GetKey(paramResult.parameterInfo);
                                var location = paramResult.Location;
                                return $"{lookupName}({location}):{paramResult.failure}";
                            })
                        .ToArray();

                    var contentFailedValidations = failedValidationErrorMessages.Any() ?
                        $"Please correct the values for [{failedValidationErrorMessages.Join(",")}]"
                        :
                        "";

                    var extraParamMessages = extraQueryParams
                        .NullToEmpty()
                        .Select(extraQueryParam => $"{extraQueryParam}(QUERY)")
                        .Concat(
                            extraBodyParams
                                .NullToEmpty()
                                .Select(extraBodyParam => $"{extraBodyParam}(BODY)"));
                    var contentExtraParams = extraParamMessages.Any() ?
                        $"emove parameters [{extraParamMessages.Join(",")}]."
                        :
                        "";

                    if (contentFailedValidations.IsNullOrWhiteSpace())
                    {
                        if (contentExtraParams.IsNullOrWhiteSpace())
                            return "Query validation failure";

                        return $"R{contentExtraParams}";
                    }

                    if (contentExtraParams.IsNullOrWhiteSpace())
                        return contentFailedValidations;

                    return $"{contentFailedValidations} and r{contentExtraParams}";
                }
            }
        }

        #endregion

    }
}
