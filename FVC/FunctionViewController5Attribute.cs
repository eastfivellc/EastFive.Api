using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class FunctionViewController5Attribute : FunctionViewController4Attribute
    {
        public override async Task<HttpResponseMessage> CreateResponseAsync(Type controllerType,
            IApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken,
            string routeName)
        {
            var matchingActionMethods = GetHttpMethods(controllerType, httpApp, request);
            if (!matchingActionMethods.Any())
                return request.CreateResponse(HttpStatusCode.NotImplemented);

            return await httpApp.GetType()
                .GetAttributesInterface<IParseContent>(true, true)
                .Where(contentParser => contentParser.DoesParse(request))
                .First(
                    (contentParser, next) =>
                    {
                        return contentParser.ParseContentValuesAsync(httpApp, request,
                            (bodyCastDelegate, bodyValues) =>
                                InvokeMethod(
                                    matchingActionMethods,
                                    httpApp, request, cancellationToken,
                                    bodyCastDelegate, bodyValues));
                    },
                    () =>
                    {
                        if(request.Content.IsDefaultOrNull())
                        {
                            CastDelegate parserEmpty =
                                (paramInfo, onParsed, onFailure) => onFailure(
                                    $"Request did not contain any content.");
                            return InvokeMethod(
                                matchingActionMethods,
                                httpApp, request, cancellationToken,
                                parserEmpty, new string[] { });
                        }
                        var mediaType = request.Content.Headers.IsDefaultOrNull() ?
                           string.Empty
                           :
                           request.Content.Headers.ContentType.IsDefaultOrNull() ?
                               string.Empty
                               :
                               request.Content.Headers.ContentType.MediaType;
                        CastDelegate parser =
                            (paramInfo, onParsed, onFailure) => onFailure(
                                $"Could not parse content of type {mediaType}");
                        return InvokeMethod(
                            matchingActionMethods,
                            httpApp, request, cancellationToken,
                            parser, new string[] { });
                    });
        }

        protected virtual IEnumerable<MethodInfo> GetHttpMethods(Type controllerType,
            IApplication httpApp, HttpRequestMessage request)
        {
            var matchingActionMethods = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .Where(
                    method =>
                    {
                        var routeMatcher = method.GetAttributesInterface<IMatchRoute>().Single();
                        return routeMatcher.IsMethodMatch(method, request, httpApp);
                    });
            return matchingActionMethods;
        }

        protected virtual async Task<HttpResponseMessage> InvokeMethod(
            IEnumerable<MethodInfo> matchingActionMethods,
            IApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken,
            CastDelegate bodyCastDelegate, string[] bodyValues)
        {
            var evaluatedMethods = matchingActionMethods
                .Select(
                    method =>
                    {
                        var routeMatcher = method.GetAttributesInterface<IMatchRoute>().Single();
                        return routeMatcher.IsRouteMatch(method, request, httpApp,
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
                                (parameterSelection, methodFinal, httpAppFinal, requestFinal) =>
                                {
                                    return InvokeValidatedMethodAsync(
                                        httpAppFinal, requestFinal, cancellationToken,
                                        methodFinal,
                                        parameterSelection);
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
                                        return validator.ValidateRequest(parameterSelection,
                                            methodCurrent,
                                            httpAppCurrent, requestCurrent,
                                            boundCallback);
                                    };
                                })
                            .Invoke(
                                new KeyValuePair<ParameterInfo, object>[] { },
                                methodCast.method, httpApp, request);
                    },
                    () =>
                    {
                        return Issues(evaluatedMethods).AsTask();
                    });

            HttpResponseMessage Issues(IEnumerable<RouteMatch> methodsCasts)
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
                    return request
                        .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                        .AddReason("No methods that implement Action");
                }
                var content = reasonStrings.Join(";");
                return request
                    .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
                    .AddReason(content);
            }
        }

        protected static Task<HttpResponseMessage> InvokeValidatedMethodAsync(
            IApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken,
            MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            return httpApp.GetType()
                .GetAttributesInterface<IHandleMethods>(true, true)
                .Aggregate<IHandleMethods, MethodHandlingDelegate>(
                    (methodFinal, queryParametersFinal, httpAppFinal, requestFinal) =>
                    {
                        var response = InvokeHandledMethodAsync(httpApp, request, cancellationToken, method, queryParameters);
                        return response;
                    },
                    (callback, methodHandler) =>
                    {
                        return (methodCurrent, queryParametersCurrent, httpAppCurrent, requestCurrent) =>
                            methodHandler.HandleMethodAsync(methodCurrent,
                                queryParametersCurrent, httpAppCurrent, requestCurrent,
                                callback);
                    })
                .Invoke(method, queryParameters, httpApp, request);
        }

        protected static Task<HttpResponseMessage> InvokeHandledMethodAsync(
            IApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken,
            MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            var queryParameterOptions = queryParameters.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
            return method.GetParameters()
                .SelectReduce(
                    async (methodParameter, next) =>
                    {
                        var instigationAttrs = methodParameter.ParameterType.GetAttributesInterface<IInstigatable>();
                        if (instigationAttrs.Any())
                        {
                            var instigationAttr = instigationAttrs.First();
                            return await instigationAttr.Instigate(httpApp as HttpApplication,
                                    request, cancellationToken,
                                    methodParameter,
                                next);
                        }

                        if (queryParameterOptions.ContainsKey(methodParameter.Name))
                            return await next(queryParameterOptions[methodParameter.Name]);

                        return await httpApp.Instigate(request, cancellationToken, methodParameter,
                            v => next(v));
                    },
                    async (object[] methodParameters) =>
                    {
                        try
                        {
                            if (method.IsGenericMethod)
                            {
                                var genericArguments = method.GetGenericArguments().Select(arg => arg.Name).Join(",");
                                return request.CreateResponse(HttpStatusCode.InternalServerError)
                                    .AddReason($"Could not invoke {method.DeclaringType.FullName}..{method.Name} because it contains generic arguments:{genericArguments}");
                            }

                            var response = method.Invoke(null, methodParameters);
                            if (typeof(HttpResponseMessage).IsAssignableFrom(method.ReturnType))
                                return ((HttpResponseMessage)response);
                            if (typeof(Task<HttpResponseMessage>).IsAssignableFrom(method.ReturnType))
                                return (await (Task<HttpResponseMessage>)response);
                            if (typeof(Task<Task<HttpResponseMessage>>).IsAssignableFrom(method.ReturnType))
                                return (await await (Task<Task<HttpResponseMessage>>)response);

                            return (request.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
                                .AddReason($"Could not convert type: {method.ReturnType.FullName} to HttpResponseMessage."));
                        }
                        catch (TargetInvocationException ex)
                        {
                            var paramList = methodParameters.Select(p => p.GetType().FullName).Join(",");
                            var body = ex.InnerException.IsDefaultOrNull() ?
                                ex.StackTrace
                                :
                                $"[{ex.InnerException.GetType().FullName}]{ex.InnerException.Message}:\n{ex.InnerException.StackTrace}";
                            return request
                                .CreateResponse(HttpStatusCode.InternalServerError, body)
                                .AddReason($"Could not invoke {method.DeclaringType.FullName}.{method.Name}({paramList})");
                        }
                        catch (Exception ex)
                        {
                            return await httpApp.GetType()
                                .GetAttributesInterface<IHandleExceptions>(true, true)
                                .Aggregate<IHandleExceptions, HandleExceptionDelegate>(
                                    (exFinal, methodFinal, queryParametersFinal, httpAppFinal, requestFinal) =>
                                    {
                                        if (ex is IHttpResponseMessageException)
                                        {
                                            var httpResponseMessageException = ex as IHttpResponseMessageException;
                                            return httpResponseMessageException.CreateResponseAsync(
                                                httpApp, request, queryParameterOptions,
                                                method, methodParameters).AsTask();
                                        }

                                        return request
                                            .CreateResponse(HttpStatusCode.InternalServerError)
                                            .AddReason(ex.Message)
                                            .AsTask();
                                    },
                                    (callback, methodHandler) =>
                                    {
                                        return (exCurrent, methodCurrent, queryParametersCurrent, httpAppCurrent, requestCurrent) =>
                                            methodHandler.HandleExceptionAsync(exCurrent, methodCurrent,
                                            queryParametersCurrent, httpAppCurrent, requestCurrent,
                                            callback);
                                    })
                                .Invoke(ex, method, queryParameters, httpApp, request);
                            
                        }

                    });
        }
    }
}
