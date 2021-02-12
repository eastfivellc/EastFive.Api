using EastFive.Api.Modules;
using EastFive.Api.Services;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Razor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api.Core
{
    public class Middleware : IAsyncDisposable
    {
        private readonly RequestDelegate continueAsync;
        private readonly IApplication app;
        private readonly IRazorViewEngine razorViewEngine;
        private readonly string[] pathLookups;
        private readonly IBackgroundTaskQueue taskQueue;

        public const string HeaderStatusName = "X-StatusName";
        public const string HeaderStatusInstance = "X-StatusInstance";

        public Middleware(RequestDelegate next, IApplication app,
            IRazorViewEngine razorViewEngine,
            IBackgroundTaskQueue taskQueue)
        {
            this.continueAsync = next;
            this.app = app;
            this.razorViewEngine = razorViewEngine;
            this.taskQueue = taskQueue;
            this.pathLookups = app.Resources
                .Select(res => res.invokeResourceAttr.Namespace)
                .Where(res => res.HasBlackSpace())
                .Distinct()
                .ToArray();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var matchesResources = pathLookups.Any(pathLookup => context.Request.Path.StartsWithSegments('/' + pathLookup));
            if (!matchesResources)
            {
                await continueAsync(context);
                return;
            }

            var requestLifetime = context.Features.Get<IHttpRequestLifetimeFeature>();
            var cancellationToken = requestLifetime.IsDefaultOrNull()?
                new CancellationToken()
                :
                requestLifetime.RequestAborted;
            var request = new CoreHttpRequest(context.Request, this.razorViewEngine, cancellationToken);
            var routeResponse = await InvokeRequestAsync(request, this.app,
                () =>
                {
                    return new HttpResponse(context, continueAsync);
                });

            context.Response.StatusCode = (int)routeResponse.StatusCode;
            routeResponse = AddReason(routeResponse);
            foreach (var header in routeResponse.Headers)
                context.Response.Headers.Add(header.Key, header.Value);

            routeResponse.WriteCookiesToResponse(context);

            await routeResponse.WriteResponseAsync(context.Response.Body);

            if (routeResponse is IHaveMoreWork)
                taskQueue.QueueBackgroundWorkItem(
                    (cancellationToken) => (routeResponse as IHaveMoreWork)
                        .ProcessWorkAsync(cancellationToken));

            IHttpResponse AddReason(IHttpResponse response)
            {
                var reason = response.ReasonPhrase;
                if (string.IsNullOrEmpty(reason))
                    return response;

                var reasonPhrase = reason.Replace('\n', ';').Replace("\r", "");
                if (reasonPhrase.Length > 510)
                    reasonPhrase = new string(reasonPhrase.Take(510).ToArray());
                
                response.SetHeader("X-Reason", reasonPhrase);

                var responseFeature = context.Features.Get<IHttpResponseFeature>();
                if (!responseFeature.IsDefaultOrNull())
                    responseFeature.ReasonPhrase = reason;

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
                get => default; 
                set => throw new NotImplementedException(); 
            }

            public IDictionary<string, string[]> Headers => new Dictionary<string, string[]>();

            public HttpResponse(RequestDelegate continueAsync)
            {
                this.continueAsync = continueAsync;
            }

            public void WriteCookie(string cookieKey, string cookieValue, TimeSpan? expireTime)
            {
                CookieOptions option = new CookieOptions();

                if (expireTime.HasValue)
                    option.Expires = DateTime.Now + expireTime.Value;
                else
                    option.Expires = DateTime.Now.AddMilliseconds(10);

                context.Response.Cookies.Append(cookieKey, cookieValue, option);
            }

            public void WriteCookiesToResponse(HttpContext context)
            {
                // Written on the fly above
            }

            public virtual Task WriteResponseAsync(Stream stream)
            {
                return continueAsync(this.context);
            }
        }

        public static Task<IHttpResponse> InvokeRequestAsync(IHttpRequest requestMessage,
            IApplication application,
            Func<IHttpResponse> skip)
        {
            var matchingResources = application.Resources
                .NullToEmpty()
                .Select(
                    resource =>
                    {
                        var doesHandleRequest = resource.invokeResourceAttr.DoesHandleRequest(
                            resource.type, requestMessage, out double matchQuality);
                        return new
                        {
                            doesHandleRequest,
                            resource,
                            matchQuality,
                        };
                    })
                .OrderBy(tpl => tpl.matchQuality)
                .Where(kvp => kvp.doesHandleRequest);

            //var debug = matchingResources.ToArray();

            return matchingResources
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
                                    var invokeResource = controllerTypeFinal
                                        .GetAttributesInterface<IInvokeResource>()
                                        .First();
                                    var response = await invokeResource
                                        .CreateResponseAsync(controllerTypeFinal,
                                            httpAppFinal, routeDataFinal);
                                    return response;
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

        ConcurrentQueue<IAsyncDisposable> asyncDisposables = new ConcurrentQueue<IAsyncDisposable>();

        public async ValueTask DisposeAsync()
        {
            while (asyncDisposables.TryDequeue(out IAsyncDisposable disposable))
                await disposable.DisposeAsync();
        }
    }
}
