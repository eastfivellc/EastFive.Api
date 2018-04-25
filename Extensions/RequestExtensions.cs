using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using BlackBarLabs.Extensions;
using BlackBarLabs.Api.Resources;
using BlackBarLabs.Linq;
using Microsoft.Azure;
using BlackBarLabs.Web;
using System.Security.Claims;
using System.Configuration;
using EastFive.Api;
using EastFive.Linq;
using EastFive.Extensions;

namespace BlackBarLabs.Api
{
    public static class RequestExtensions
    {
        public static async Task<IHttpActionResult> GetPossibleMultipartResponseAsync<TResource>(this HttpRequestMessage request,
            IEnumerable<TResource> query,
            Func<TResource, Task<HttpResponseMessage>> singlepart,
            Func<HttpActionDelegate> ifEmpty = default(Func<HttpActionDelegate>))
        {
            if ((!query.Any()) && (!ifEmpty.IsDefaultOrNull()))
            {
                return ifEmpty().ToActionResult();
            }

            var queryTasks = query.Select(resource => singlepart(resource));
            var queryResponses = await Task.WhenAll(queryTasks);
            if (queryResponses.Length == 1)
                return queryResponses[0].ToActionResult();

            return await request.CreateMultipartActionAsync(queryResponses);
        }

        public static async Task<IHttpActionResult> CreateMultipartActionAsync(this HttpRequestMessage request,
            IEnumerable<HttpResponseMessage> contents)
        {
            return (await request.CreateMultipartResponseAsync(contents)).ToActionResult();
        }

        public static async Task<HttpResponseMessage> CreateMultipartResponseAsync(this HttpRequestMessage request,
            IEnumerable<HttpResponseMessage> contents)
        {
            if (request.Headers.Accept.Contains(accept => accept.MediaType.ToLower().Contains("multipart/mixed")))
            {
                return request.CreateHttpMultipartResponse(contents);
            }

            return await request.CreateBrowserMultipartResponse(contents);
        }

        private static HttpResponseMessage CreateHttpMultipartResponse(this HttpRequestMessage request,
            IEnumerable<HttpResponseMessage> contents)
        {
            var multipartContent = new MultipartContent("mixed", "----Boundary_" + Guid.NewGuid().ToString("N"));
            request.CreateResponse(HttpStatusCode.OK, multipartContent);
            foreach (var content in contents)
            {
                multipartContent.Add(new HttpMessageContent(content));
            }
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = multipartContent;
            return response;
        }

        private static async Task<HttpResponseMessage> CreateBrowserMultipartResponse(this HttpRequestMessage request,
            IEnumerable<HttpResponseMessage> contents)
        {
            var multipartContentTasks = contents.NullToEmpty().Select(
                async (content) =>
                {
                    return await content.Content.HasValue(
                        async (contentContent) =>
                        {
                            var response = new Response
                            {
                                StatusCode = content.StatusCode,
                                ContentType = contentContent.Headers.ContentType,
                                ContentLocation = contentContent.Headers.ContentLocation,
                                Content = await contentContent.ReadAsStringAsync(),
                                ReasonPhrase = content.ReasonPhrase,
                                Location = content.Headers.Location,
                            };
                            return response;
                        },
                        () =>
                        {
                            var response = new Response
                            {
                                StatusCode = content.StatusCode,
                                ReasonPhrase = content.ReasonPhrase,
                                Location = content.Headers.Location,
                            };
                            return Task.FromResult(response);
                        });
                });

            var multipartContents = await Task.WhenAll(multipartContentTasks);
            var multipartResponseContent = new MultipartResponse
            {
                StatusCode = HttpStatusCode.OK,
                Content = multipartContents,
                Location = request.RequestUri,
            };

            var multipartResponse = request.CreateResponse(HttpStatusCode.OK, multipartResponseContent);
            multipartResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-multipart+json");
            return multipartResponse;
        }

        [Obsolete("User ParseAsync")]
        public static IHttpActionResult MergeIds<TResource>(this HttpRequestMessage request, Guid idUrl, TResource resource,
            Func<TResource, HttpActionDelegate> actionCallback,
            Func<Guid, WebId> createIdCallback)
            where TResource : ResourceBase
        {
            return resource.Id.GetUUID<IHttpActionResult>(
                (resourceId) => idUrl.HasValue<IHttpActionResult>(
                    (resourceIdUrl) =>
                    {
                        // Id's are specified in both places, ensure they match
                        if (resourceId != resourceIdUrl)
                            return request.CreateResponse(
                                    HttpStatusCode.BadRequest, "Incorrect URL for resource")
                                .ToActionResult();
                        var action = actionCallback(resource);
                        return action.ToActionResult();
                    },
                    () =>
                    {
                        // the URL id was not used, but the body has one,
                        // just do the call standard
                        HttpActionDelegate action = actionCallback(resource);
                        return action.ToActionResult();
                    }),
                () => idUrl.HasValue<IHttpActionResult>(
                    (resourceId) =>
                    {
                        // Only the URL has an id, 
                        // construct a resource with ID specified and return it.
                        var resourceWithId = resource.HasValue(
                            (value) => value,
                            () => Activator.CreateInstance<TResource>());
                        resourceWithId.Id = createIdCallback(resourceId);
                        HttpActionDelegate action = actionCallback(resource);
                        return action.ToActionResult();
                    },
                    () => request.CreateResponse(
                            HttpStatusCode.BadRequest, "No resource specified")
                        .ToActionResult()));
        }

        public static TResult HasSiteAdminAuthorization<TResult>(this AuthenticationHeaderValue authorizationHeader,
            Func<TResult> isAuthorized,
            Func<string, TResult> notAuthorized)
        {
            var result = authorizationHeader.HasValue(
                value =>
                {
                    return EastFive.Web.Configuration.Settings.GetString(AppSettings.SiteAdminAuthorization,
                        siteAdminAuthorization =>
                        {
                            if (!string.IsNullOrEmpty(siteAdminAuthorization) && siteAdminAuthorization == value.ToString())
                                return isAuthorized();
                            return notAuthorized("This account is not authorized to create accounts");
                        },
                        (why) => notAuthorized("No site admin authorization has been configured"));
                },
                () => notAuthorized("This account is not authorized to create accounts"));
            return result;
        }

        public static TResult GetClaimsFromAuthorizationHeader<TResult>(this AuthenticationHeaderValue header,
            Func<IEnumerable<Claim>, TResult> success,
            Func<TResult> authorizationNotSet,
            Func<string, TResult> failure,
            string issuerConfigSetting = EastFive.Security.AppSettings.TokenIssuer,
            string validationKeyConfigSetting = EastFive.Security.AppSettings.TokenKey)
        {
            if (default(AuthenticationHeaderValue) == header)
                return authorizationNotSet();
            var jwtString = header.ToString();
            return jwtString.GetClaimsFromJwtToken(success, authorizationNotSet, failure, issuerConfigSetting, validationKeyConfigSetting);
        }

        public static TResult GetClaimsFromJwtToken<TResult>(this string jwtString,
            Func<IEnumerable<Claim>, TResult> success,
            Func<TResult> authorizationNotSet,
            Func<string, TResult> failure,
            string issuerConfigSetting = EastFive.Security.AppSettings.TokenIssuer,
            string validationKeyConfigSetting = EastFive.Security.AppSettings.TokenKey)
        {
            if (String.IsNullOrWhiteSpace(jwtString))
                return authorizationNotSet();
            return jwtString.GetClaimsJwtString(
                success,
                (why) =>
                {
                    var siteAdminAuthorization = CloudConfigurationManager.GetSetting(
                        EastFive.Api.AppSettings.SiteAdminAuthorization);

                    if (string.IsNullOrEmpty(siteAdminAuthorization))
                        return failure(why); //TODO - log if this is not set?

                    if (String.Compare(siteAdminAuthorization, jwtString, false) != 0)
                        return failure(why);

                    return EastFive.Web.Configuration.Settings.GetString(
                        EastFive.Api.AppSettings.ActorIdClaimType,
                        (actorIdClaimType) =>
                        {
                            return EastFive.Web.Configuration.Settings.GetString(
                                EastFive.Api.AppSettings.ActorIdSuperAdmin,
                                (actorIdSuperAdmin) =>
                                {
                                    var claim = new Claim(actorIdClaimType, actorIdSuperAdmin);
                                    return success(claim.AsArray());
                                },
                                failure);
                        },
                        failure);
                },
                issuerConfigSetting,
                validationKeyConfigSetting);
        }

        public static TResult GetClaims<TResult>(this HttpRequestMessage request,
            Func<IEnumerable<System.Security.Claims.Claim>, TResult> success,
            Func<TResult> authorizationNotSet,
            Func<string, TResult> failure)
        {
            if (request.IsDefaultOrNull())
                return authorizationNotSet();
            if (request.Headers.IsDefaultOrNull())
                return authorizationNotSet();
            var result = request.Headers.Authorization.GetClaimsFromAuthorizationHeader(
                success, authorizationNotSet, failure,
                EastFive.Security.AppSettings.TokenIssuer, EastFive.Security.AppSettings.TokenKey);
            return result;
        }

        public static Task<HttpResponseMessage> GetClaimsAsync(this HttpRequestMessage request,
            Func<System.Security.Claims.Claim[], Task<HttpResponseMessage>> success)
        {
            var result = request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    return success(claims);
                },
                () => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized).AddReason("Authorization header not set").ToTask(),
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized).AddReason(why).ToTask());
            return result;
        }

        public static Task<HttpResponseMessage[]> GetClaimsAsync(this HttpRequestMessage request,
            Func<System.Security.Claims.Claim[], Task<HttpResponseMessage[]>> success)
        {
            var result = request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    return success(claims);
                },
                () => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized).AddReason("Authorization header not set")
                    .AsEnumerable().ToArray().ToTask(),
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized).AddReason(why)
                    .AsEnumerable().ToArray().ToTask());
            return result;
        }

        public static Task<HttpResponseMessage> GetActorIdClaimsFromTokenAsync(this HttpRequestMessage request, string jwtToken,
            Func<Guid, System.Security.Claims.Claim[], Task<HttpResponseMessage>> success)
        {
            return jwtToken.GetClaimsJwtString(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    var accountIdClaimType =
                        ConfigurationManager.AppSettings[EastFive.Api.AppSettings.ActorIdClaimType];
                    var result = claims.GetAccountIdAsync(
                        request, accountIdClaimType,
                        (accountId) => success(accountId, claims));
                    return result;
                },
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason("Authorization header not set").ToTask());
        }

        public static Task<HttpResponseMessage> GetSessionIdClaimsAsync(this HttpRequestMessage request,
            Func<Guid, System.Security.Claims.Claim[], Task<HttpResponseMessage>> success)
        {
            var sessionIdClaimTypeConfigurationSetting =
                EastFive.Api.AppSettings.SessionIdClaimType;
            return GetSessionIdClaimsAsync(request, sessionIdClaimTypeConfigurationSetting, success);
        }

        public static Task<HttpResponseMessage> GetSessionIdClaimsAsync(this HttpRequestMessage request,
            string sessionIdClaimTypeConfigurationSetting,
            Func<Guid, System.Security.Claims.Claim[], Task<HttpResponseMessage>> success)
        {
            var resultGetClaims = request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    //var accountIdClaimType =
                    //    ConfigurationManager.AppSettings[sessionIdClaimTypeConfigurationSetting];
                    var sessionIdClaimType = Security.ClaimIds.Session;
                    var result = claims.GetSessionIdAsync(
                        request, sessionIdClaimType,
                        (sessionId) => success(sessionId, claims));
                    return result;
                },
                () => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason("Authorization header not set").ToTask(),
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason(why).ToTask());
            return resultGetClaims;
        }
        
        public static HttpResponseMessage GetActorIdClaims(this HttpRequestMessage request,
            Func<Guid, System.Security.Claims.Claim[], HttpResponseMessage> success)
        {
            var accountIdClaimTypeConfigurationSetting =
                EastFive.Api.AppSettings.ActorIdClaimType;
            return GetActorIdClaims(request, accountIdClaimTypeConfigurationSetting, success);
        }

        public static HttpResponseMessage GetActorIdClaims(this HttpRequestMessage request,
            string accountIdClaimTypeConfigurationSetting,
            Func<Guid, System.Security.Claims.Claim[], HttpResponseMessage> success)
        {
            var resultGetClaims = request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    var accountIdClaimType =
                        ConfigurationManager.AppSettings[accountIdClaimTypeConfigurationSetting];
                    var result = claims.GetAccountId(
                        request, accountIdClaimType,
                        (accountId) => success(accountId, claims));
                    return result;
                },
                () => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason("Authorization header not set"),
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason(why));
            return resultGetClaims;
        }

        public static Task<HttpResponseMessage> GetActorIdClaimsAsync(this HttpRequestMessage request,
            Func<Guid, System.Security.Claims.Claim[], Task<HttpResponseMessage>> success)
        {
            var accountIdClaimTypeConfigurationSetting =
                EastFive.Api.AppSettings.ActorIdClaimType;
            return GetActorIdClaimsAsync(request, accountIdClaimTypeConfigurationSetting, success);
        }

        public static Task<HttpResponseMessage> GetActorIdClaimsAsync(this HttpRequestMessage request,
            string accountIdClaimTypeConfigurationSetting,
            Func<Guid, System.Security.Claims.Claim[], Task<HttpResponseMessage>> success)
        {
            var resultGetClaims = request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    var accountIdClaimType = 
                        ConfigurationManager.AppSettings[accountIdClaimTypeConfigurationSetting];
                    var result = claims.GetAccountIdAsync(
                        request, accountIdClaimType,
                        (accountId) => success(accountId, claims));
                    return result;
                },
                () => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason("Authorization header not set").ToTask(),
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason(why).ToTask());
            return resultGetClaims;
        }

        public static Task<HttpResponseMessage[]> GetActorIdClaimsAsync(this HttpRequestMessage request,
            string accountIdClaimType,
            Func<Guid, System.Security.Claims.Claim[], Task<HttpResponseMessage[]>> success)
        {
            var resultGetClaims = request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    var result = claims.GetAccountIdAsync(
                        request, accountIdClaimType,
                        (accountId) => success(accountId, claims));
                    return result;
                },
                () => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason("Authorization header not set").AsEnumerable().ToArray().ToTask(),
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason(why).AsEnumerable().ToArray().ToTask());
            return resultGetClaims;
        }

        public static Task<HttpResponseMessage[]> GetActorIdClaimsAsync(this HttpRequestMessage request,
           Func<Guid, System.Security.Claims.Claim[], Task<HttpResponseMessage[]>> success)
        {
            var accountIdClaimTypeConfigurationSetting =
                EastFive.Api.AppSettings.ActorIdClaimType;
            return EastFive.Web.Configuration.Settings.GetString(
                accountIdClaimTypeConfigurationSetting,
                (accountIdClaimType) =>
                    GetActorIdClaimsAsync(request, accountIdClaimType, success),
                (error) =>
                    (new HttpResponseMessage[]
                    {
                        request
                            .CreateResponse(HttpStatusCode.Unauthorized)
                            .AddReason(error)
                    }).ToTask());
        }
    }
}
