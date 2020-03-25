using EastFive.Api.Modules;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api.Core
{
    public class Middleware
    {
        private readonly RequestDelegate continueAsync;
        private readonly IApplication app;

        public const string HeaderStatusName = "X-StatusName";
        public const string HeaderStatusInstance = "X-StatusInstance";

        public Middleware(RequestDelegate next, IApplication app)
        {
            this.continueAsync = next;
            this.app = app;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var routeResponse = await InvokeRequestAsync(context.Request, this.app, 
                () =>
                {
                    var routeResponse = new IHttpResponse()
                    {
                        writeResultAsync = (context) => continueAsync(context),
                    };
                    return routeResponse.AsTask();
                });
            await routeResponse.writeResultAsync(context);
        }

        public static Task<IHttpResponse> InvokeRequestAsync(HttpRequest requestMessage,
            IApplication application,
            Func<Task<IHttpResponse>> continueAsync)
        {
            return application.Resources
                .NullToEmpty()
                .Select(
                    resource =>
                    {
                        var doesHandleRequest = resource.invokeResourceAttr.DoesHandleRequest(
                            resource.type, requestMessage, out IHttpRequest routeData);
                        return new
                        {
                            doesHandleRequest,
                            resource,
                            routeData,
                        };
                    })
                .Where(kvp => kvp.doesHandleRequest)
                .First(
                    async (requestHandler, next) =>
                    {
                        var resource = requestHandler.resource;
                        var cancellationToken = new CancellationToken();
                        var extensionMethods = requestHandler.resource.extensions;

                        var response = await application.GetType()
                            .GetAttributesInterface<IHandleRoutes>(true, true)
                            .Aggregate<IHandleRoutes, RouteHandlingDelegate>(
                                async (controllerTypeFinal, httpAppFinal, routeDataFinal) =>
                                {
                                    var invokeResource = controllerTypeFinal.GetAttributesInterface<IInvokeResource>().First();
                                    var response = await invokeResource.CreateResponseAsync(controllerTypeFinal,
                                        httpAppFinal, routeDataFinal, cancellationToken);
                                    return response;

                                    //return await resource.invokeResourceAttr.CreateResponseAsync(resource.type,
                                    //    this.app, httpRequestMessage, cancellationToken,
                                    //    requestHandler.routeData, extensionMethods);
                                },
                                (callback, routeHandler) =>
                                {
                                    return (controllerTypeCurrent, httpAppCurrent, routeNameCurrent) =>
                                        routeHandler.HandleRouteAsync(controllerTypeCurrent,
                                            httpAppCurrent, routeNameCurrent,
                                            callback);
                                })
                            .Invoke(resource.type, application, requestHandler.routeData);

                        return response;
                    },
                    continueAsync);
        }

    }
}
