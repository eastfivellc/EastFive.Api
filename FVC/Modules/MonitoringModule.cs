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

        private Task<HttpResponseMessage> StoreMonitoringInfoAndFireRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken, Guid authenticationId)
        {
            return GetControllerNameAndId(request, 
                async (controllerName, iden) =>
                {
                    var queryString = GetParamInfo(request, iden);

                    Task createLogTask = MonitoringDocument.CreateAsync(storageAppSettingKey, Guid.NewGuid(), authenticationId,
                        DateTime.UtcNow, request.Method.ToString(), controllerName, queryString, () => true);

                    var httpResponseMessage = await base.SendAsync(request, cancellationToken);

                    await createLogTask;
                    return httpResponseMessage;
                },
                ()=>
                {
                    return default(HttpResponseMessage).ToTask();
                });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpResponseMessage = await request.GetActorIdClaimsFromBearerParamAsync(
                async (authenticationId, claims) =>
                {
                    return await StoreMonitoringInfoAndFireRequestAsync(request, cancellationToken, authenticationId);
                },
                async () =>
                {
                    return await request.GetActorIdClaimsAsync(
                        async (authenticationId, claims) =>
                        {
                            return await StoreMonitoringInfoAndFireRequestAsync(request, cancellationToken, authenticationId);
                        });
                },
                () =>
                {
                    return default(HttpResponseMessage).ToTask();
                },
                () =>
                {
                    return default(HttpResponseMessage).ToTask();
                });
            if (httpResponseMessage.IsDefaultOrNull())
                return await base.SendAsync(request, cancellationToken);
            return httpResponseMessage;
        }

        private TResult GetControllerNameAndId<TResult>(HttpRequestMessage request,
            Func<string, string, TResult> onSuccess,
            Func<TResult> onUndetermined)
        {
            // controller and id
            if (request.RequestUri.Segments.Length == 4)
                return onSuccess(request.RequestUri.Segments[2], request.RequestUri.Segments[3]);

            // just controller
            if (request.RequestUri.Segments.Length == 3 )
                return onSuccess(request.RequestUri.Segments[2], string.Empty);

            if (request.RequestUri.Segments.Length == 2)
                return onSuccess(request.RequestUri.Segments[1], string.Empty);

            if (request.RequestUri.Segments.Length == 1)
                return onSuccess(request.RequestUri.Segments[0], string.Empty);

            return onUndetermined();
        }

        private string GetParamInfo(HttpRequestMessage request, string iden)
        {
            var queryParams = request.GetQueryNameValuePairs();
            var queryElements = queryParams.Select(qP => $"{qP.Key}:{qP.Value}");

            if (!iden.IsNullOrWhiteSpace())
                queryElements = queryElements.Concat(new[] { $"Id:{iden}" });

            return queryElements.Join(" | ");
        }
    }
}
