using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using Microsoft.ApplicationInsights.DataContracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class FunctionViewController5Attribute : FunctionViewController4Attribute
    {
        public override async Task<HttpResponseMessage> CreateResponseAsync(Type controllerType,
            IApplication httpApp, HttpRequestMessage request, string routeName)
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

            if (!matchingActionMethods.Any())
                return request.CreateResponse(HttpStatusCode.NotImplemented);

            return await httpApp.ParseContentValuesAsync<SelectParameterResult, HttpResponseMessage>(request.Content,
                async (bodyParser, bodyValues) =>
                {
                    CastDelegate<SelectParameterResult> bodyCastDelegate =
                        (paramInfo, onParsed, onFailure) =>
                        {
                            return bodyParser(paramInfo, httpApp, request,
                                value =>
                                {
                                    var parsedResult = onParsed(value);
                                    return parsedResult;
                                },
                                (why) => onFailure(why));
                        };

                    //var debugConsider = await methodsForConsideration.ToArrayAsync();
                    var evaluatedMethods = matchingActionMethods
                        .Select(
                            method =>
                            {
                                var routeMatcher = method.GetAttributesInterface<IMatchRoute>().Single();
                                return routeMatcher.IsRouteMatch(method, request, httpApp,
                                    bodyValues, bodyCastDelegate);
                            })
                        .AsyncEnumerable();

                    var validMethods = evaluatedMethods
                        .Where(methodCast => methodCast.isValid);

                    //var debug = await evaluatedMethods.ToArrayAsync();
                    return await await validMethods
                        .FirstAsync(
                            (methodCast) =>
                            {
                                return InvokeValidatedMethodAsync(httpApp, request, methodCast.method,
                                    methodCast.parametersWithValues);
                            },
                            () =>
                            {
                                return Issues(evaluatedMethods);
                            });

                    async Task<HttpResponseMessage> Issues(IEnumerableAsync<RouteMatch> methodsCasts)
                    {
                        var reasonStrings = await methodsCasts
                            .Select(
                                methodCast =>
                                {
                                    var errorMessage = methodCast.ErrorMessage;
                                    return errorMessage;
                                })
                            .ToArrayAsync();
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
                });
        }

        protected static Task<HttpResponseMessage> InvokeValidatedMethodAsync(
            IApplication httpApp, HttpRequestMessage request, MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            return httpApp.GetType()
                .GetAttributesInterface<IHandleMethods>(true, true)
                .Aggregate<IHandleMethods, MethodHandlingDelegate>(
                    (methodFinal, queryParametersFinal, httpAppFinal, requestFinal) =>
                    {
                        var response = InvokeHandledMethodAsync(httpApp, request, method, queryParameters);
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
            IApplication httpApp, HttpRequestMessage request, MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            var queryParameterOptions = queryParameters.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
            return method.GetParameters()
                .SelectReduce(
                    async (methodParameter, next) =>
                    {
                        if (queryParameterOptions.ContainsKey(methodParameter.Name))
                            return await next(queryParameterOptions[methodParameter.Name]);

                        return await httpApp.Instigate(request, methodParameter,
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
                            if (ex is IHttpResponseMessageException)
                            {
                                var httpResponseMessageException = ex as IHttpResponseMessageException;
                                return httpResponseMessageException.CreateResponseAsync(
                                    httpApp, request, queryParameterOptions,
                                    method, methodParameters);
                            }
                            // TODO: Only do this in a development environment
                            return request.CreateResponse(HttpStatusCode.InternalServerError, ex.StackTrace).AddReason(ex.Message);
                        }

                    });
        }
    }
}
