using BlackBarLabs.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class UnauthorizedException : Exception, IHttpResponseMessageException
    {
        public UnauthorizedException() : base()
        {
        }

        public UnauthorizedException(string message) : base(message)
        {
        }

        public HttpResponseMessage CreateResponseAsync(IApplication httpApp,
           HttpRequestMessage request, Dictionary<string, object> queryParameterOptions,
           MethodInfo method, object[] methodParameters)
        {
            var response = request.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            response.Content = new StringContent(this.StackTrace);
            return response.AddReason(this.Message);
        }
    }
}
