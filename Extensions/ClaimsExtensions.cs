using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using BlackBarLabs.Web;
using EastFive.Web.Configuration;
using Microsoft.Azure;

namespace BlackBarLabs.Api
{
    public static class ClaimsExtensions
    {
        public static Task<HttpResponseMessage> GetSessionIdAsync(this IEnumerable<System.Security.Claims.Claim> claims,
            HttpRequestMessage request, string sessionIdClaimType,
            Func<Guid, Task<HttpResponseMessage>> success)
        {
            var adminClaim = claims
                .FirstOrDefault((claim) => String.Compare(claim.Type, sessionIdClaimType) == 0);

            if (default(System.Security.Claims.Claim) == adminClaim)
                return request.CreateResponse(HttpStatusCode.Unauthorized).ToTask();

            var accountId = Guid.Parse(adminClaim.Value);
            return success(accountId);
        }

        public static HttpResponseMessage GetAccountId(this IEnumerable<System.Security.Claims.Claim> claims,
            HttpRequestMessage request, string accountIdClaimType,
            Func<Guid, HttpResponseMessage> success)
        {
            var adminClaim = claims
                .FirstOrDefault((claim) => String.Compare(claim.Type, accountIdClaimType) == 0);

            if (default(System.Security.Claims.Claim) == adminClaim)
                return request.CreateResponse(HttpStatusCode.Unauthorized);

            var accountId = Guid.Parse(adminClaim.Value);
            return success(accountId);
        }

        public static Task<HttpResponseMessage> GetAccountIdAsync(this IEnumerable<System.Security.Claims.Claim> claims,
            HttpRequestMessage request, string accountIdClaimType,
            Func<Guid, Task<HttpResponseMessage>> success)
        {
            var adminClaim = claims
                .FirstOrDefault((claim) => String.Compare(claim.Type, accountIdClaimType) == 0);

            if (default(System.Security.Claims.Claim) == adminClaim)
                return request.CreateResponse(HttpStatusCode.Unauthorized).ToTask();

            var accountId = Guid.Parse(adminClaim.Value);
            return success(accountId);
        }

        public static TResult GetAccountIdMaybe<TResult>(this IEnumerable<System.Security.Claims.Claim> claims,
            HttpRequestMessage request, string accountIdClaimType,
            Func<Guid?, TResult> success)
        {
            var adminClaim = claims
                .FirstOrDefault((claim) => String.Compare(claim.Type, accountIdClaimType) == 0);

            if (default(System.Security.Claims.Claim) == adminClaim)
                return success(default(Guid?));

            var accountId = Guid.Parse(adminClaim.Value);
            return success(accountId);
        }

        public static Task<HttpResponseMessage[]> GetAccountIdAsync(this IEnumerable<System.Security.Claims.Claim> claims, HttpRequestMessage request, string accountIdClaimType,
            Func<Guid, Task<HttpResponseMessage[]>> success)
        {
            var adminClaim = claims
                .FirstOrDefault((claim) => String.Compare(claim.Type, accountIdClaimType) == 0);

            if (default(System.Security.Claims.Claim) == adminClaim)
                return request.CreateResponse(HttpStatusCode.Unauthorized).AsEnumerable().ToArray().ToTask();

            var accountId = Guid.Parse(adminClaim.Value);
            return success(accountId);
        }

        public static TResult GetActorId<TResult>(this IEnumerable<System.Security.Claims.Claim> claims,
            Func<Guid, TResult> success,
            Func<TResult> actorIdNotFound)
        {
            var accountIdClaimTypeConfigurationSetting =
                EastFive.Api.AppSettings.ActorIdClaimType;
            return claims.GetActorId(accountIdClaimTypeConfigurationSetting, success, actorIdNotFound);
        }

        public static TResult GetActorId<TResult>(this IEnumerable<System.Security.Claims.Claim> claims, 
            string accountIdClaimType,
            Func<Guid, TResult> success,
            Func<TResult> actorIdNotFound)
        {
            //var accountIdClaimValue = CloudConfigurationManager.GetSetting(accountIdClaimType);
            return accountIdClaimType.ConfigurationString(
                accountIdClaimValue =>
                {
                    //TODO - Log if not found

                    var adminClaim = claims
                        .FirstOrDefault((claim) => String.Compare(claim.Type, accountIdClaimValue) == 0);

                    if (default(System.Security.Claims.Claim) == adminClaim)
                        return actorIdNotFound();

                    var accountId = Guid.Parse(adminClaim.Value);
                    return success(accountId);
                }); // ConfigurationContext.Instance.AppSettings[accountIdClaimType];

        }
    }
}
