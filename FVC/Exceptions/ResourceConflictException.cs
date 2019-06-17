using BlackBarLabs.Api;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;

namespace EastFive.Api
{

    public class ResourceConflictException : ArgumentException, IHttpResponseMessageException
    {
        public ResourceConflictException() : base()
        {

        }

        public ResourceConflictException(string message) : base(message)
        {

        }

        public ResourceConflictException(string message, Exception innerException) : base(message, innerException)
        {

        }

        public ResourceConflictException(string message, string paramName) : base(message, paramName)
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
