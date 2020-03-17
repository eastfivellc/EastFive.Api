﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public struct RouteData
    {
        public string[] pathParameters;
        public string action;
        public string route;
        public string ns;
    }

    public delegate Task<HttpResponseMessage> RouteHandlingDelegate(Type controllerType,
            IApplication httpApp, HttpRequestMessage request,
            RouteData routeData, MethodInfo[] extensionMethods);

    public interface IHandleRoutes
    {
        Task<HttpResponseMessage> HandleRouteAsync(Type controllerType,
            IApplication httpApp, HttpRequestMessage request,
            RouteData routeData, MethodInfo[] extensionMethods,
            RouteHandlingDelegate continueExecution);
    }
}
