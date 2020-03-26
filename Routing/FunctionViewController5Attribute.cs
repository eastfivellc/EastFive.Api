using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Api.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EastFive.Api
{
    public class FunctionViewController5Attribute : FunctionViewController4Attribute
    {
        public async override Task<IHttpResponse> CreateResponseAsync(Type controllerType,
            IApplication httpApp, IHttpRequest request)
        {
            var matchingActionMethods = GetHttpMethods(controllerType,
                httpApp, request);
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
                                    httpApp, request,
                                    bodyCastDelegate, bodyValues));
                    },
                    () =>
                    {
                        if(request.Body.IsDefaultOrNull())
                        {
                            CastDelegate parserEmpty =
                                (paramInfo, onParsed, onFailure) => onFailure(
                                    $"Request did not contain any content.");
                            return InvokeMethod(
                                matchingActionMethods,
                                httpApp, request,
                                parserEmpty, new string[] { });
                        }
                        var mediaType = request.GetMediaType();
                        CastDelegate parser =
                            (paramInfo, onParsed, onFailure) => onFailure(
                                $"Could not parse content of type {mediaType}");
                        return InvokeMethod(
                            matchingActionMethods,
                            httpApp, request, 
                            parser, new string[] { });
                    });
        }

        protected virtual IEnumerable<MethodInfo> GetHttpMethods(Type controllerType,
            IApplication httpApp, IHttpRequest request)
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

        protected virtual async Task<IHttpResponse> InvokeMethod(
            IEnumerable<MethodInfo> matchingActionMethods,
            IApplication httpApp, IHttpRequest routeData,
            CastDelegate bodyCastDelegate, string[] bodyValues)
        {
            var evaluatedMethods = matchingActionMethods
                .Select(
                    method =>
                    {
                        var routeMatcher = method.GetAttributesInterface<IMatchRoute>().Single();
                        return routeMatcher.IsRouteMatch(method, this, routeData, httpApp,
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
                                                    methodFinal,
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

        protected static Task<IHttpResponse> InvokeValidatedMethodAsync(
            IApplication httpApp, IHttpRequest routeData,
            MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            return httpApp.GetType()
                .GetAttributesInterface<IHandleMethods>(true, true)
                .Aggregate<IHandleMethods, MethodHandlingDelegate>(
                    (methodFinal, queryParametersFinal, httpAppFinal, routeDataFinal) =>
                    {
                        var response = InvokeHandledMethodAsync(httpApp, routeDataFinal, method, queryParameters);
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
            MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            var queryParameterOptions = queryParameters.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
            return method.GetParameters()
                .SelectReduce(
                    async (ParameterInfo methodParameter, Func<object, Task<IHttpResponse>> next) =>
                    {
                        if (queryParameterOptions.ContainsKey(methodParameter.Name))
                            return await next(queryParameterOptions[methodParameter.Name]);

                        return await httpApp.Instigate(routeData, methodParameter,
                            next);
                    },
                    async (object[] methodParameters) =>
                    {
                        try
                        {
                            if (method.IsGenericMethod)
                            {
                                var genericArguments = method.GetGenericArguments().Select(arg => arg.Name).Join(",");
                                return routeData.CreateResponse(HttpStatusCode.InternalServerError)
                                    .AddReason($"Could not invoke {method.DeclaringType.FullName}..{method.Name} because it contains generic arguments:{genericArguments}");
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
                            var paramList = methodParameters.Select(p => p.GetType().FullName).Join(",");
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
    }
}
