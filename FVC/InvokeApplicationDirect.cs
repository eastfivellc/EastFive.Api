using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using EastFive.Api.Modules;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api
{
    [InvokeApplicationDirect.Instigate]
    public class InvokeApplicationDirect : InvokeApplication
    {
        public InvokeApplicationDirect(IApplication application, Uri serverUrl, string apiRouteName, System.Threading.CancellationToken token) 
            : base(serverUrl, apiRouteName)
        {
            this.application = application;
            this.token = token;
        }

        private readonly IApplication application;
        private readonly System.Threading.CancellationToken token;

        public override IApplication Application => application;

        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequest)
        {
            return ControllerHandler.DirectSendAsync(application, httpRequest, 
                token,
                (requestBack, token) =>
                {
                    throw new Exception();
                });
        }

        public class InstigateAttribute : Attribute, IInstigatable
        {
            public Task<HttpResponseMessage> Instigate(IApplication httpApp, 
                    HttpRequestMessage request, CancellationToken cancellationToken, 
                    ParameterInfo parameterInfo,
                Func<object, Task<HttpResponseMessage>> onSuccess)
            {
                string GetApiPrefix()
                {
                    return "api";
                }
                var instance = new InvokeApplicationDirect(httpApp, request.RequestUri, GetApiPrefix(), default(CancellationToken));
                return onSuccess(instance);
            }
        }
    }
}
