using Microsoft.AspNetCore.Http;
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
    public delegate Task<IHttpResponse> RouteHandlingDelegate(Type controllerType,
            IApplication httpApp, IHttpRequest routeData);

    public static class RouteResponseExtensions
    {
        public static IHttpResponse CreateResponse(this IHttpRequest request, HttpStatusCode statusCode)
        {
            return new HttpResponse(request, statusCode);
        }

        public static IHttpResponse CreateResponse<T>(this IHttpRequest request,
            HttpStatusCode statusCode,
            T content)
        {
            return new JsonHttpResponse<T>(request, statusCode, content);
        }

        public static IHttpResponse AddReason(this IHttpResponse routeResponse,
            string reasonText)
        {
            routeResponse.ReasonPhrase = reasonText;
            return routeResponse;
        }

        public static IHttpResponse CreateExtrudedResponse(this IHttpRequest routeData,
            HttpStatusCode statusCode,object[] contents)
        {
        }
    }

    public interface IHandleRoutes
    {
        Task<IHttpResponse> HandleRouteAsync(Type controllerType,
            IApplication httpApp, IHttpRequest routeData,
            RouteHandlingDelegate continueExecution);
    }
}
