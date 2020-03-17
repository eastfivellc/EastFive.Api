using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Api.Resources;
using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using BlackBarLabs.Api;

namespace EastFive.Api
{
    [StatusCodeResponse(StatusCode = HttpStatusCode.Accepted)]
    public delegate HttpResponseMessage AcceptedResponse();
    
    [AcceptedBodyResponse]
    public delegate HttpResponseMessage AcceptedBodyResponse(object content, string contentType = default(string));
    public class AcceptedBodyResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Accepted;

        public override string Example => "serialized object";

        public override Task<HttpResponseMessage> InstigateInternal(IApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            AcceptedBodyResponse responseDelegate =
                (obj, contentType) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.Accepted, obj);
                    if (!contentType.IsNullOrWhiteSpace())
                        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess(responseDelegate);
        }
    }

    public interface IExecuteAsync
    {
        bool ForceBackground { get; }

        Task<HttpResponseMessage> InvokeAsync(Action<double> updateCallback);
    }

    [ExecuteBackgroundResponse]
    public delegate Task<HttpResponseMessage> ExecuteBackgroundResponseAsync(IExecuteAsync executeAsync);
    public class ExecuteBackgroundResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Accepted;

        public override string Example => "serialized object";

        public override Task<HttpResponseMessage> InstigateInternal(IApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ExecuteBackgroundResponseAsync responseDelegate =
                async (executionContext) =>
                {
                    var responseInvoke = await executionContext.InvokeAsync(v => { });
                    return UpdateResponse(parameterInfo, httpApp, request, responseInvoke);
                };
            return onSuccess(responseDelegate);
        }
    }
}
