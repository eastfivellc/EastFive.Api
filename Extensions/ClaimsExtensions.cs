using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Web.Configuration;

namespace EastFive.Api
{
    public static class ClaimsExtensions
    {
        public static Task<IHttpResponse> GetSessionIdAsync(this IEnumerable<System.Security.Claims.Claim> claims,
                IHttpRequest request, string sessionIdClaimType,
            Func<Guid, Task<IHttpResponse>> success)
        {
            return claims
                .Where(claim => String.Compare(claim.Type, sessionIdClaimType) == 0)
                .First(
                    (adminClaim, next) =>
                    {
                        var accountId = Guid.Parse(adminClaim.Value);
                        return success(accountId);
                    },
                    () => request.CreateResponse(HttpStatusCode.Unauthorized).AsTask());

            
        }

        public static IHttpResponse GetAccountId(this IEnumerable<System.Security.Claims.Claim> claims,
                IHttpRequest request, string accountIdClaimType,
            Func<Guid, IHttpResponse> success)
        {
            var adminClaim = claims
                .FirstOrDefault((claim) => String.Compare(claim.Type, accountIdClaimType) == 0);

            if (default(System.Security.Claims.Claim) == adminClaim)
                return request.CreateResponse(HttpStatusCode.Unauthorized);

            var accountId = Guid.Parse(adminClaim.Value);
            return success(accountId);
        }

        public static Task<IHttpResponse> GetAccountIdAsync(this IEnumerable<System.Security.Claims.Claim> claims,
            IHttpRequest request, string accountIdClaimType,
            Func<Guid, Task<IHttpResponse>> success)
        {
            var adminClaim = claims
                .FirstOrDefault((claim) => String.Compare(claim.Type, accountIdClaimType) == 0);

            if (default(System.Security.Claims.Claim) == adminClaim)
                return request.CreateResponse(HttpStatusCode.Unauthorized).AsTask();

            var accountId = Guid.Parse(adminClaim.Value);
            return success(accountId);
        }

        public static TResult GetAccountIdMaybe<TResult>(this IEnumerable<System.Security.Claims.Claim> claims,
            IHttpRequest request, string accountIdClaimType,
            Func<Guid?, TResult> success)
        {
            var adminClaim = claims
                .FirstOrDefault((claim) => String.Compare(claim.Type, accountIdClaimType) == 0);

            if (default(System.Security.Claims.Claim) == adminClaim)
                return success(default(Guid?));

            var accountId = Guid.Parse(adminClaim.Value);
            return success(accountId);
        }

        public static Task<IHttpResponse[]> GetAccountIdAsync(this IEnumerable<System.Security.Claims.Claim> claims,
                IHttpRequest request, string accountIdClaimType,
            Func<Guid, Task<IHttpResponse[]>> success)
        {
            var adminClaim = claims
                .FirstOrDefault((claim) => String.Compare(claim.Type, accountIdClaimType) == 0);

            if (default(System.Security.Claims.Claim) == adminClaim)
                return request.CreateResponse(HttpStatusCode.Unauthorized).AsEnumerable().ToArray().AsTask();

            var accountId = Guid.Parse(adminClaim.Value);
            return success(accountId);
        }

        public static TResult GetSessionId<TResult>(this IEnumerable<System.Security.Claims.Claim> claims,
            Func<Guid, TResult> success,
            Func<TResult> actorIdNotFound)
        {
            var sessionIdClaimTypeConfigurationSetting =
                EastFive.Api.AppSettings.SessionIdClaimType;
            return claims.GetSessionId(sessionIdClaimTypeConfigurationSetting, success, actorIdNotFound);
        }

        public static TResult GetSessionId<TResult>(this IEnumerable<System.Security.Claims.Claim> claims,
            string sessionIdClaimType,
            Func<Guid, TResult> success,
            Func<TResult> sessionIdNotFound)
        {
            return sessionIdClaimType.ConfigurationString(
                accountIdClaimValue =>
                {
                    var adminClaim = claims
                        .FirstOrDefault((claim) => String.Compare(claim.Type, accountIdClaimValue) == 0);

                    if (default(System.Security.Claims.Claim) == adminClaim)
                        return sessionIdNotFound();

                    var accountId = Guid.Parse(adminClaim.Value);
                    return success(accountId);
                });
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
