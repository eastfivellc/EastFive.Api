﻿using EastFive.Extensions;
using EastFive.Web.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class CorsHandlerAttribute : Attribute, IHandleRoutes
    {
        public Task<HttpResponseMessage> HandleRouteAsync(Type controllerType,
            IApplication httpApp, HttpRequestMessage request,
            RouteData routeData, MethodInfo[] extensionMethods, 
            RouteHandlingDelegate continueExecution)
        {
            Func<Task<HttpResponseMessage>> skip = 
                () => continueExecution(controllerType, httpApp, request,
                    routeData, extensionMethods);
            
            if(!AppSettings.CorsCorrection.ConfigurationBoolean(s =>s, onNotSpecified:() => false))
                return skip();

            return GetDecoratedResponse();

            Task<HttpResponseMessage> GetResponse()
            {
                if (request.Method.Method.ToLower() != HttpMethod.Options.Method.ToLower())
                    return skip();

                if (!request.Headers.Contains("Access-Control-Request-Headers"))
                    return skip();

                var response = request.CreateResponse(System.Net.HttpStatusCode.OK);
                return response.AsTask();
            }

            async Task<HttpResponseMessage> GetDecoratedResponse()
            {
                var response = await GetResponse();
                if (!response.Headers.Contains("Access-Control-Allow-Origin"))
                    response.Headers.Add("Access-Control-Allow-Origin", "*");
                if (!response.Headers.Contains("Access-Control-Allow-Headers"))
                    response.Headers.Add("Access-Control-Allow-Headers", "*");
                if (!response.Headers.Contains("Access-Control-Allow-Methods"))
                    response.Headers.Add("Access-Control-Allow-Methods", "*");
                return response;
            }
        }
    }
}
