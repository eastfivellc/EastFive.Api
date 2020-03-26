using EastFive.Api.Modules;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class InstigatableResponseAttribute : Attribute
    {
        protected IHttpResponse UpdateResponse(ParameterInfo parameterInfo,
            IApplication httpApp, IHttpRequest request,
            IHttpResponse response)
        {
            response.Headers.Add(Core.Middleware.HeaderStatusName, parameterInfo.ParameterType.FullName.AsArray());
            response.Headers.Add(Core.Middleware.HeaderStatusInstance, parameterInfo.Name.AsArray());
            return httpApp.GetType()
                .GetAttributesInterface<IHandleResponses>(true, true)
                .Aggregate<IHandleResponses, ResponseHandlingDelegate>(
                    (param, app, req, responseFinal) => responseFinal,
                    (callback, responseHandler) =>
                    {
                        return (param, app, req, resp) =>
                            responseHandler.HandleResponse(param,
                                app, req, resp,
                                callback);
                    })
                .Invoke(parameterInfo, httpApp, request, response);
        }


        protected class HttpResponse : EastFive.Api.HttpResponse
        {
            private Func<Stream, Task> writeResponseAsync;

            public HttpResponse(IHttpRequest request, HttpStatusCode statusCode, Func<Stream, Task> writeResponseAsync)
                : base(request, statusCode)
            {
                this.writeResponseAsync = writeResponseAsync;
            }

            public override Task WriteResponseAsync(Stream responseStream)
            {
                return writeResponseAsync(responseStream);
            }
        }

        
    }
}
