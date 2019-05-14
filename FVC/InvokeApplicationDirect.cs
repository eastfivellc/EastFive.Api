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

        protected override RequestMessage<TResource> BuildRequest<TResource>(IApplication application, HttpRequestMessage httpRequest)
        {
            return new RequestMessage<TResource>(application, httpRequest);
        }

    }

    
}
