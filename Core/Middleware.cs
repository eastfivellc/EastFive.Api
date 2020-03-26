using EastFive.Api.Modules;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
            var request = new CoreHttpRequest(context.Request, new CancellationToken());
            var routeResponse = await InvokeRequestAsync(request, this.app, 
                () =>
                {
                    return new HttpResponse(context, continueAsync);
                });
            context.Response.StatusCode = (int)routeResponse.StatusCode;
            routeResponse = AddReason(routeResponse);
            foreach (var header in routeResponse.Headers)
                context.Response.Headers.Add(header.Key, header.Value);

            await routeResponse.WriteResponseAsync(context.Response.Body);

            IHttpResponse AddReason(IHttpResponse response)
            {
                var reason = response.ReasonPhrase;
                if (string.IsNullOrEmpty(reason))
                    return response;

                var reasonPhrase = reason.Replace('\n', ';').Replace("\r", "");
                if (reasonPhrase.Length > 510)
                    reasonPhrase = new string(reasonPhrase.Take(510).ToArray());
                
                response.SetHeader("X-Reason", reasonPhrase);

                //if (response.StatusCode == HttpStatusCode.Unauthorized)
                //    response.WriteResponseAsync = (stream) => JsonHttpResponse<int>.WriteResponseAsync(
                //        stream, new { Message = reason })
                //    {
                //        var messageResponse = JsonConvert.SerializeObject();

                //    };
                return response;
            }
        }


        private class HttpResponse : IHttpResponse
        {
            private readonly RequestDelegate continueAsync;
            private readonly HttpContext context;

            public HttpResponse(HttpContext context, RequestDelegate continueAsync)
            {
                this.context = context;
                this.continueAsync = continueAsync;
            }

            public IHttpRequest Request => throw new NotImplementedException();

            public HttpStatusCode StatusCode { get; set; }

            public string ReasonPhrase 
            {
                get => throw new NotImplementedException(); 
                set => throw new NotImplementedException(); 
            }

            public IDictionary<string, string[]> Headers => throw new NotImplementedException();

            public HttpResponse(RequestDelegate continueAsync)
            {
                this.continueAsync = continueAsync;
            }

            public virtual Task WriteResponseAsync(System.IO.Stream responseStream)
            {
                return continueAsync(context);
            }
        }

        public static Task<IHttpResponse> InvokeRequestAsync(IHttpRequest requestMessage,
            IApplication application,
            Func<IHttpResponse> skip)
        {
            return application.Resources
                .NullToEmpty()
                .Select(
                    resource =>
                    {
                        var doesHandleRequest = resource.invokeResourceAttr.DoesHandleRequest(
                            resource.type, requestMessage);
                        return new
                        {
                            doesHandleRequest,
                            resource,
                        };
                    })
                .Where(kvp => kvp.doesHandleRequest)
                .First(
                    async (requestHandler, next) =>
                    {
                        var resource = requestHandler.resource;
                        var extensionMethods = requestHandler.resource.extensions;

                        var response = await application.GetType()
                            .GetAttributesInterface<IHandleRoutes>(true, true)
                            .Aggregate<IHandleRoutes, RouteHandlingDelegate>(
                                async (controllerTypeFinal, httpAppFinal, routeDataFinal) =>
                                {
                                    var invokeResource = controllerTypeFinal.GetAttributesInterface<IInvokeResource>().First();
                                    var response = await invokeResource.CreateResponseAsync(controllerTypeFinal,
                                        httpAppFinal, routeDataFinal);
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
                            .Invoke(resource.type, application, requestMessage);

                        return response;
                    },
                    skip.AsAsyncFunc());
        }

    }
}
