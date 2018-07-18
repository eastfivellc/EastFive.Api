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
using BlackBarLabs.Api.Monitoring;
using System.Web.Http.Routing;
using System.Web.Http.Controllers;

namespace EastFive.Api.Modules
{
    public class MonitoringModule : System.Net.Http.DelegatingHandler
    {
        private System.Web.Http.HttpConfiguration config;
        private string storageAppSettingKey;

        public MonitoringModule(System.Web.Http.HttpConfiguration config, string storageAppSettingKey)
        {
            this.storageAppSettingKey = storageAppSettingKey;
            this.config = config;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpResponseMessage = default(HttpResponseMessage);
            HttpResponseMessage maybeResponse = await request.GetActorIdClaimsAsync(
                async (authenticationId, claims) =>
                {
                    var controllerName = GetControllerName(request);
                    Task createLogTask = MonitoringDocument.CreateAsync(storageAppSettingKey, Guid.NewGuid(), authenticationId, "", 
                        DateTime.UtcNow, request.Method.ToString(), controllerName, request.Content.ToString(), ()=> true);

                    httpResponseMessage = await base.SendAsync(request, cancellationToken);

                    await createLogTask;
                    return httpResponseMessage;
                });
            if (httpResponseMessage.IsDefaultOrNull())
                return await base.SendAsync(request, cancellationToken);
            return httpResponseMessage;
        }

        private string GetControllerName(HttpRequestMessage request)
        {
            var attributedRoutesData = request.GetRouteData().GetSubRoutes();
            var subRouteData = attributedRoutesData.FirstOrDefault();

            var actions = (ReflectedHttpActionDescriptor[])subRouteData.Route.DataTokens["actions"];
            var controllerName = actions[0].ControllerDescriptor.ControllerName;
            return controllerName;
        }

    }
}
