using EastFive.Extensions;
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
            Func < Task < HttpResponseMessage >> skip = () => continueExecution(controllerType, httpApp, request, routeName);
            if (request.Method.Method.ToLower() != HttpMethod.Options.Method.ToLower())
                return skip();

            if (!request.Headers.Contains("Access-Control-Request-Headers"))
                return skip();

            var response = request.CreateResponse(System.Net.HttpStatusCode.OK);
            //response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            return response.AsTask();
        }
    }
}
