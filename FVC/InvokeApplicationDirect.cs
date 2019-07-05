using EastFive.Api.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace EastFive.Api
{
    public class InvokeApplicationDirect : InvokeApplication
    {
        public InvokeApplicationDirect(IApplication application, Uri serverUrl, string apiRouteName) 
            : base(serverUrl, apiRouteName)
        {
            this.application = application;
        }

        private IApplication application;
        public override IApplication Application => application;

        public override Task<HttpResponseMessage> SendAsync<TResource>(
            RequestMessage<TResource> requestMessage, HttpRequestMessage httpRequest)
        {
            return ControllerHandler.DirectSendAsync(application, httpRequest, 
                default(CancellationToken),
                (requestBack, token) =>
                {
                    throw new Exception();
                });
        }


    }

    
}
