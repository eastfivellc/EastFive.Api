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
            IApplication httpApp, IHttpRequest request,
            RouteHandlingDelegate continueExecution)
        {
            Func<Task<IHttpResponse>> skip = 
                () => continueExecution(controllerType, httpApp, request);
            
            if(!AppSettings.CorsCorrection.ConfigurationBoolean(s =>s, onNotSpecified:() => false))
                return skip();

            if (request.Method.Method.ToLower() != HttpMethod.Options.Method.ToLower())
                return skip();

            if (!request.TryGetHeader("Access-Control-Request-Headers", out string accessControlRequestHeader))
                return skip();

            var response = request.CreateResponse(System.Net.HttpStatusCode.OK);
            if (!response.Headers.ContainsKey("Access-Control-Allow-Origin"))
                response.SetHeader("Access-Control-Allow-Origin", "*");
            if (!response.Headers.ContainsKey("Access-Control-Allow-Headers"))
                response.SetHeader("Access-Control-Allow-Headers", "*");
            if (!response.Headers.ContainsKey("Access-Control-Allow-Methods"))
                response.SetHeader("Access-Control-Allow-Methods", "*");

            return response.AsTask();
        }
    }
}
