using EastFive.Extensions;
using EastFive.Web.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class CorsHandlerAttribute : Attribute, IHandleRoutes
    {
        public Task<HttpResponseMessage> HandleRouteAsync(Type controllerType,
            IApplication httpApp, HttpRequestMessage request, 
            string routeName, RouteHandlingDelegate continueExecution)
        {
            Func<Task<HttpResponseMessage>> skip = 
                () => continueExecution(controllerType, httpApp, request, routeName);
            
            if(!AppSettings.CorsCorrection.ConfigurationBoolean(s =>s, onNotSpecified:() => false))
                return skip();

            if (request.Method.Method.ToLower() != HttpMethod.Options.Method.ToLower())
                return skip();

            if (!request.Headers.Contains("Access-Control-Request-Headers"))
                return skip();

            var response = request.CreateResponse(System.Net.HttpStatusCode.OK);
            if (!response.Headers.Contains("Access-Control-Allow-Origin"))
                response.Headers.Add("Access-Control-Allow-Origin", "*");
            if (!response.Headers.Contains("Access-Control-Allow-Headers"))
                response.Headers.Add("Access-Control-Allow-Headers", "*");
            return response.AsTask();
        }
    }
}
