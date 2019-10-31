using BlackBarLabs.Api;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [SessionToken]
    public struct SessionToken
    {
        public Guid sessionId;
        public Claim[] claims;
        public Guid? accountIdMaybe;
    }
    public class SessionTokenAttribute : Attribute, IInstigatable
    {
        public Task<HttpResponseMessage> Instigate(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            return EastFive.Web.Configuration.Settings.GetString(AppSettings.ActorIdClaimType,
                (accountIdClaimType) =>
                {
                    return request.GetClaims(
                        (claimsEnumerable) =>
                        {
                            var claims = claimsEnumerable.ToArray();
                            return claims.GetAccountIdMaybe(
                                    request, accountIdClaimType,
                                (accountIdMaybe) =>
                                {
                                    var sessionIdClaimType = BlackBarLabs.Security.ClaimIds.Session;
                                    return claims.GetSessionIdAsync(
                                        request, sessionIdClaimType,
                                        (sessionId) =>
                                        {
                                            var token = new SessionToken
                                            {
                                                accountIdMaybe = accountIdMaybe,
                                                sessionId = sessionId,
                                                claims = claims,
                                            };
                                            return onSuccess(token);
                                        });
                                });
                        },
                        () => request
                            .CreateResponse(HttpStatusCode.Unauthorized)
                            .AddReason("Authorization header not set.")
                            .AsTask(),
                        (why) => request
                            .CreateResponse(HttpStatusCode.Unauthorized)
                            .AddReason(why)
                            .AsTask());
                },
                (why) => request.CreateResponse(HttpStatusCode.Unauthorized).AddReason(why).AsTask());

        }
    }
}
