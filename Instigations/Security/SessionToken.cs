using BlackBarLabs.Api;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Web.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [SessionToken]
    public struct SessionToken
    {
        public Guid sessionId;
        public Claim[] claims;
        public Guid? accountIdMaybe;

        public static Guid? GetClaimIdMaybe(IEnumerable<Claim> claims,
            string claimType)
        {
            return claims.First(
                (claim, next) =>
                {
                    if (String.Compare(claim.Type, claimType) != 0)
                        return next();
                    var accountId = Guid.Parse(claim.Value);
                    return accountId;
                },
                () => default(Guid?));
        }
    }

    public class SessionTokenAttribute : Attribute, IInstigatable
    {
        public Task<HttpResponseMessage> Instigate(IApplication httpApp,
                HttpRequestMessage request, CancellationToken cancellationToken, 
                ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            var accountIdClaimType = Auth.ClaimEnableActorAttribute.Type;
            return request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    return claims.GetAccountIdMaybe(
                            request, accountIdClaimType,
                        (accountIdMaybe) =>
                        {
                            var sessionIdClaimType = Auth.ClaimEnableSessionAttribute.Type;
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
        }
    }
}
