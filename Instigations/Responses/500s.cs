using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Reflection;
using Microsoft.ApplicationInsights.DataContracts;

namespace EastFive.Api
{
    [GeneralFailureResponse]
    public delegate HttpResponseMessage GeneralFailureResponse(string why = default);
    public class GeneralFailureResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.InternalServerError;

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            GeneralFailureResponse responseDelegate =
                (why) =>
                {
                    var response = request.CreateResponse(StatusCode);
                    if (why.IsDefaultNullOrEmpty())
                        return response;
                    return response.AddReason(why);
                };
            return onSuccess(responseDelegate);
        }
    }

    [StatusCodeResponse(StatusCode = HttpStatusCode.NotImplemented)]
    public delegate HttpResponseMessage NotImplementedResponse();

    [StatusCodeResponse(StatusCode = HttpStatusCode.ServiceUnavailable)]
    public delegate HttpResponseMessage ServiceUnavailableResponse();

    [ConfigurationFailureResponse]
    public delegate HttpResponseMessage ConfigurationFailureResponse(string configurationValue, string message);
    public class ConfigurationFailureResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.ServiceUnavailable;

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ConfigurationFailureResponse responseDelegate =
                (configurationValue, message) => request
                    .CreateResponse(System.Net.HttpStatusCode.ServiceUnavailable)
                    .AddReason($"`{configurationValue}` not specified in config:{message}");
            return onSuccess(responseDelegate);
        }
    }
}
