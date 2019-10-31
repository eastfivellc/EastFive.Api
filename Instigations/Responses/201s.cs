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
    [StatusCodeResponse(StatusCode = HttpStatusCode.Created)]
    public delegate HttpResponseMessage CreatedResponse();
    public class StatusCodeResponseAttribute : HttpFuncDelegateAttribute
    {
        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            Func<HttpResponseMessage> responseFunc = 
                () => request.CreateResponse(this.StatusCode);
            var responseDelegate = responseFunc.MakeDelegate(parameterInfo.ParameterType);
            return onSuccess(responseDelegate);
        }
    }

    [ContentTypeResponse(StatusCode = HttpStatusCode.Created)]
    public delegate HttpResponseMessage CreatedBodyResponse<TResource>(object content, string contentType = default);
    
}
