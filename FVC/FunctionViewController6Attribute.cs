using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;

namespace EastFive.Api
{
    public class FunctionViewController6Attribute : FunctionViewController5Attribute
    {
        public override async Task<HttpResponseMessage> CreateResponseAsync(Type controllerType,
            IApplication httpApp, HttpRequestMessage request, string routeName)
        {
            var matchingActionMethods = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(httpApp.GetExtensionMethods(controllerType))
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
                        (parameterInfo, onParsed, onFailure) =>
                        {
                            return bodyParser(parameterInfo,
                                    httpApp, request,
                                value =>
                                {
                                    var parsedResult = onParsed(value);
                                    return parsedResult;
                                },
                                (why) => onFailure(why));
                        };

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

        public override Route GetRoute(Type type, HttpApplication httpApp)
        {
            var actionMethods = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(httpApp.GetExtensionMethods(type))
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .ToArray();

            return new Route(this.Route, 
                actionMethods,
                type.GetMembers(BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance),
                httpApp);
        }

    }
}
