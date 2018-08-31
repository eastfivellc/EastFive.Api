using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using EastFive.Serialization;
using System.Net.NetworkInformation;
using EastFive.Extensions;
using BlackBarLabs.Web;
using System.Reflection;
using System.Net.Http;
using EastFive.Linq;
using System.Net;
using BlackBarLabs.Api;
using BlackBarLabs;
using System.Threading;

namespace EastFive.Api.Modules
{
    public abstract class ApplicationHandler : System.Net.Http.DelegatingHandler
    {
        protected System.Web.Http.HttpConfiguration config;

        public ApplicationHandler(System.Web.Http.HttpConfiguration config)
        {
            this.config = config;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return request.GetApplication(
                httpApp => SendAsync(httpApp, request, cancellationToken, (requestBase, cancellationTokenBase)=> base.SendAsync(requestBase, cancellationTokenBase)),
                () => base.SendAsync(request, cancellationToken));
        }

        protected abstract Task<HttpResponseMessage> SendAsync(HttpApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> continuation);
    }
}
