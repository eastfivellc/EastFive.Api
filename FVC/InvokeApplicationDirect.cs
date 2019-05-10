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
    public abstract class InvokeApplicationDirect : InvokeApplication
    {
        public InvokeApplicationDirect(Uri serverUrl) : base(serverUrl)
        {
        }

        protected override RequestMessage<TResource> BuildRequest<TResource>(IApplication application)
        {
            return new RequestMessage<TResource>(
                async (httpRequest) =>
                {
                    var response = await EastFive.Api.Modules.ControllerHandler.DirectSendAsync(application, httpRequest, default(CancellationToken),
                        (requestBack, token) =>
                        {
                            throw new Exception($"Failed to invoke `{httpRequest.RequestUri}`");
                        });
                    return response;
                });
        }

    }

    
}
