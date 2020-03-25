using EastFive.Extensions;
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
        public Task<IHttpResponse> HandleRouteAsync(Type controllerType,
            IApplication httpApp, IHttpRequest routeData,
            RouteHandlingDelegate continueExecution)
        {
            Func<Task<IHttpResponse>> skip = 
                () => continueExecution(controllerType, httpApp, routeData);
            
            if(!AppSettings.CorsCorrection.ConfigurationBoolean(s =>s, onNotSpecified:() => false))
                return skip();

            var request = routeData.request;
            if (request.Method.ToLower() != HttpMethod.Options.Method.ToLower())
                return skip();

            if (!request.Headers.ContainsKey("Access-Control-Request-Headers"))
                return skip();

            var response = request.HttpContext.Response;
            response.StatusCode = (int)System.Net.HttpStatusCode.OK;
            if (!response.Headers.ContainsKey("Access-Control-Allow-Origin"))
                response.Headers.Add("Access-Control-Allow-Origin", "*");
            if (!response.Headers.ContainsKey("Access-Control-Allow-Headers"))
                response.Headers.Add("Access-Control-Allow-Headers", "*");
            if (!response.Headers.ContainsKey("Access-Control-Allow-Methods"))
                response.Headers.Add("Access-Control-Allow-Methods", "*");

            return new IHttpResponse()
            {

            }.AsTask();
        }
    }
}
