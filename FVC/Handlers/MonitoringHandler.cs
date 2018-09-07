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
using System.Web.Http.Routing;
using System.Web.Http.Controllers;

namespace EastFive.Api.Modules
{
    public class MonitoringHandler : ApplicationHandler
    {
        public MonitoringHandler(System.Web.Http.HttpConfiguration config)
            : base(config)
        {
        }

        private Task<HttpResponseMessage> StoreMonitoringInfoAndFireRequestAsync(HttpApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken, Guid authenticationId,
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> continuation)
        {
            return GetControllerNameAndId(request, 
                async (controllerName, iden) =>
                {
                    var queryString = GetParamInfo(request, iden);
                    
                    return await httpApp.DoesStoreMonitoring(
                        async (monitoringCallback) =>
                        {
                            var createLogTask = monitoringCallback(Guid.NewGuid(), authenticationId,
                                DateTime.UtcNow, request.Method.ToString(), controllerName, queryString);

                            var httpResponseMessage = await continuation(request, cancellationToken);

                            await createLogTask;
                            return httpResponseMessage;
                        },
                        () => continuation(request, cancellationToken));
                },
                ()=> continuation(request, cancellationToken));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpApplication httpApplication, HttpRequestMessage request, CancellationToken cancellationToken,
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> continuation)
        {
            return request.GetActorIdClaimsFromBearerParamAsync(
                (authenticationId, claims) => StoreMonitoringInfoAndFireRequestAsync(
                    httpApplication, request, cancellationToken, authenticationId, continuation),
                () =>
                {
                    return request.GetClaims(
                        (claimsEnumerable) =>
                        {
                            var claims = claimsEnumerable.ToArray();
                            var accountIdClaimTypeConfigurationSetting = EastFive.Api.AppSettings.ActorIdClaimType;
                            var accountIdClaimType = System.Configuration.ConfigurationManager.AppSettings[accountIdClaimTypeConfigurationSetting];
                            var result = claims.GetAccountIdAsync(
                                request, accountIdClaimType,
                                (authenticationId) => StoreMonitoringInfoAndFireRequestAsync(httpApplication, request, cancellationToken, authenticationId, continuation));
                            return result;
                        },
                        () => continuation(request, cancellationToken),
                        (why) => continuation(request, cancellationToken));
                },
                () => continuation(request, cancellationToken),
                () => continuation(request, cancellationToken));
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
