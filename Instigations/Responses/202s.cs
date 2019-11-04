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

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
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

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ExecuteBackgroundResponseAsync responseDelegate =
                async (executionContext) =>
                {
                    bool shouldRunInBackground()
                    {
                        if (executionContext.ForceBackground)
                            return true;

                        if (request.Headers.Accept.Contains(mediaType => mediaType.MediaType.ToLower().Contains("background")))
                            return true;

                        return false;
                    }

                    if (shouldRunInBackground())
                    {
                        var urlHelper = request.GetUrlHelper();
                        var processId = Controllers.BackgroundProgressController.CreateProcess(
                            async updateCallback =>
                            {
                                var completion = await executionContext.InvokeAsync(
                                    v =>
                                    {
                                        updateCallback(v);
                                    });
                                return completion;
                            }, 1.0);
                        var response = request.CreateResponse(HttpStatusCode.Accepted);
                        response.Headers.Add("Access-Control-Expose-Headers", "x-backgroundprocess");
                        response.Headers.Add("x-backgroundprocess", urlHelper.GetLocation<Controllers.BackgroundProgressController>(processId).AbsoluteUri);
                        return UpdateResponse(parameterInfo, httpApp, request, response);
                    }
                    var responseInvoke = await executionContext.InvokeAsync(v => { });
                    return UpdateResponse(parameterInfo, httpApp, request, responseInvoke);
                };
            return onSuccess(responseDelegate);
        }
    }
}
