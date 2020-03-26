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

namespace EastFive.Api
{
    [RedirectResponse]
    public delegate IHttpResponse RedirectResponse(Uri redirectLocation);
    public class RedirectResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Redirect;

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            RedirectResponse responseDelegate =
                (redirectLocation) =>
                {
                    var response = request.CreateRedirectResponse(redirectLocation);
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess(responseDelegate);
        }
    }

    [StatusCodeResponse(StatusCode = HttpStatusCode.NotModified)]
    public delegate HttpResponseMessage NotModifiedResponse();
}
