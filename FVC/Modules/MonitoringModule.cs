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
    public class MonitoringModule : System.Net.Http.DelegatingHandler
    {
        private System.Web.Http.HttpConfiguration config;

        public MonitoringModule(System.Web.Http.HttpConfiguration config)
        {
            this.config = config;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpResponseMessage = default(HttpResponseMessage);
            HttpResponseMessage maybeResponse = await request.GetActorIdClaimsAsync(
                async (authenticationId, claims) =>
                {
                    Task foo = default(Task); // Write params to AST
                    httpResponseMessage = await base.SendAsync(request, cancellationToken);
                    await foo;
                    return httpResponseMessage;
                });
            if (httpResponseMessage.IsDefaultOrNull())
                return await base.SendAsync(request, cancellationToken);
            return httpResponseMessage;
        }

    }
}
