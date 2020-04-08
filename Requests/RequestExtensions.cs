﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using System.Configuration;

using Microsoft.AspNetCore.Http;

using EastFive.Api;
using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Web;
using EastFive.Linq.Async;
using EastFive.Web.Configuration;

namespace EastFive.Api
{
    public static class RequestExtensions
    {
        public static Uri GetAbsoluteUri(this IHttpRequest req)
            => req.RequestUri;

        //public static string GetHeader(this IHttpRequest req, string headerKey)
        //    => req.GetHeaders(headerKey)
        //    .First(
        //        (v, next) => v,
        //        () => string.Empty);

        public static bool TryGetHeader(this IHttpRequest req, string headerKey, out string headerValue)
        {
            var headers = req.GetHeaders(headerKey);
            if(!headers.Any())
            {
                headerValue = default;
                return false;
            };
            headerValue = req.GetHeader(headerKey);
            return true;
        }

        #region Media Type

        private const string HeaderKeyContentType = "Content-Type";

        public static string GetMediaType(this IHttpRequest req)
            => req.GetHeader(HeaderKeyContentType);

        public static IEnumerable<MediaTypeWithQualityHeaderValue> GetMediaTypes(this IHttpRequest req)
            => req
                .GetHeaders(HeaderKeyContentType)
                .Select(acceptString => new MediaTypeWithQualityHeaderValue(acceptString));

        public static bool TryGetMediaType(this IHttpRequest req, out string mediaType)
            => req.TryGetHeader(HeaderKeyContentType, out mediaType);

        public static bool TryGetMediaType(this IHttpRequest req, out MediaTypeWithQualityHeaderValue mediaType)
        {
            var mediaTypes = req.GetMediaTypes();
            if (mediaTypes.Any())
            {
                mediaType = mediaTypes.First();
                return true;
            }
            mediaType = default;
            return false;
        }

        public static void SetMediaType(this IHttpRequest req, string contentType)
            => req
                .UpdateHeader(
                    HeaderKeyContentType, 
                    x => x.Append(contentType).ToArray());

        #endregion

        private const string HeaderKeyAcceptCharset = "Accept-Charset";

        public static bool TryGetAcceptCharset(this IHttpRequest req, out System.Text.Encoding encoding)
        {
            var encodingStrings = req
                .GetHeaders(HeaderKeyAcceptCharset)
                .SelectMany(encodingsString => encodingsString.Split(','))
                .Where(
                    encodingStringUntrimmed =>
                    {
                        var encodingString = encodingStringUntrimmed.Trim();
                        if (encodingString.Equals("br", StringComparison.OrdinalIgnoreCase))
                            return false;

                        if (encodingString.Equals("gzip", StringComparison.OrdinalIgnoreCase))
                            return false;

                        if (encodingString.Equals("deflate", StringComparison.OrdinalIgnoreCase))
                            return false;

                        try
                        {
                            System.Text.Encoding.GetEncoding(encodingString);
                            return true;
                        } catch (ArgumentException)
                        {
                            return false;
                        }
                    });
            if(!encodingStrings.Any())
            {
                encoding = new System.Text.UTF8Encoding(false);
                return false;
            }
            encoding = System.Text.Encoding.GetEncoding(encodingStrings.First());
            return true;
        }

        #region Authorization

        public static string GetAuthorization(this IHttpRequest req)
            => req.GetHeader("Authorization");

        public static bool TryGetAuthorization(this IHttpRequest req, out string authorization)
            => req.TryGetHeader("Authorization", out authorization);

        #endregion

        #region Accepts

        public static IEnumerable<MediaTypeWithQualityHeaderValue> GetAcceptTypes(this IHttpRequest req)
            => req.GetHeaders("accept")
            .SelectMany(acceptString => acceptString.Split(','))
            .Select(acceptString => new MediaTypeWithQualityHeaderValue(acceptString));

        public static IEnumerable<StringWithQualityHeaderValue> GetAcceptLanguage(this IHttpRequest req)
            => req.GetHeaders("Accept-Language")
            .Select(acceptString => new StringWithQualityHeaderValue(acceptString));

        #endregion

        public static void SetReferer(this IHttpRequest req, Uri referer)
            => req.UpdateHeader("Referer", x => x.Append(referer.AbsoluteUri).ToArray());

        public static bool TryGetReferer(this IHttpRequest req, out Uri referer)
        {
            if (req.TryGetHeader("Referer", out string refererString))
                return Uri.TryCreate(refererString, UriKind.RelativeOrAbsolute, out referer);

            referer = default;
            return false;
        }

        public static bool IsJson(this IHttpRequest req)
            => req.GetMediaType().ToLower().Contains("json");

        public static bool IsMimeMultipartContent(this IHttpRequest req)
            => req.GetMediaType().ToLower().StartsWith("multipart/");

        public static bool IsXml(this IHttpRequest req)
            => req.GetMediaType().ToLower().Contains("xml");

        public static bool IsAuthorizedFor(this IHttpRequest request,
            string claimType)
        {
            var jwtString = request.GetAuthorization();
            if (jwtString.IsNullOrWhiteSpace())
                return false;
            return jwtString.GetClaimsJwtString(
                claims =>
                {
                    return claims.First(
                        (claim, next) =>
                        {
                            if (String.Compare(claim.Type, claimType) == 0)
                                return true;
                            return next();
                        },
                        () => false);
                },
                (why) => false);
        }

        public static bool IsAuthorizedFor(this IHttpRequest request,
            Uri claimType, string claimValue)
        {
            var jwtString = request.GetAuthorization();
            if (jwtString.IsNullOrWhiteSpace())
                return false;
            return jwtString.GetClaimsJwtString(
                claims =>
                {
                    return claims.First(
                        (claim, next) =>
                        {
                            if (String.Compare(claim.Type, claimType.OriginalString) == 0)
                            {
                                if(claim.Value.Split(','.AsArray()).Contains(claimValue))
                                    return true;
                            }
                            return next();
                        },
                        () => false);
                },
                (why) => false);
        }

        public static bool TryParseJwt(this IHttpRequest request,
            out System.IdentityModel.Tokens.Jwt.JwtSecurityToken securityToken)
        {
            securityToken = default;
            var jwtString = request.GetAuthorization();
            
            if (jwtString.IsNullOrWhiteSpace())
                return false;

            var kvp = jwtString.ParseJwtString(
                st => st.PairWithKey(true),
                (why) => default);

            securityToken = kvp.Value;
            return kvp.Key;
        }

        public static bool IsContentOfType(this IHttpRequest request, string contentType)
        {
            if(!request.TryGetMediaType(out string requestContentType))
                return contentType.IsNullOrWhiteSpace();

            return requestContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase);
        }

        #region Claims / Auth

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
                    //var siteAdminAuthorization = CloudConfigurationManager.GetSetting(
                    //    EastFive.Api.AppSettings.SiteAdminAuthorization);
                    return EastFive.Api.AppSettings.SiteAdminAuthorization.ConfigurationString(
                        siteAdminAuthorization =>
                        {
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
                        });
                },
                issuerConfigSetting,
                validationKeyConfigSetting);
        }

        public static TResult GetClaims<TResult>(this IHttpRequest request,
            Func<IEnumerable<System.Security.Claims.Claim>, TResult> success,
            Func<TResult> authorizationNotSet,
            Func<string, TResult> failure)
        {
            if(!request.TryGetAuthorization(out string jwtString))
                return authorizationNotSet();

            return jwtString.GetClaimsFromJwtToken(success, authorizationNotSet, failure);
            
        }

        public static Task<IHttpResponse> GetClaimsAsync(this IHttpRequest request,
            Func<System.Security.Claims.Claim[], Task<IHttpResponse>> success)
        {
            var result = request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    return success(claims);
                },
                () => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized).AddReason("Authorization header not set").AsTask(),
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized).AddReason(why).AsTask());
            return result;
        }

        public static Task<IHttpResponse[]> GetClaimsAsync(this IHttpRequest request,
            Func<System.Security.Claims.Claim[], Task<IHttpResponse[]>> success)
        {
            var result = request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    return success(claims);
                },
                () => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized).AddReason("Authorization header not set")
                    .AsEnumerable().ToArray().AsTask(),
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized).AddReason(why)
                    .AsEnumerable().ToArray().AsTask());
            return result;
        }

        public static Task<IHttpResponse> GetActorIdClaimsFromTokenAsync(this IHttpRequest request, string jwtToken,
            Func<Guid, System.Security.Claims.Claim[], Task<IHttpResponse>> success)
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
                    .AddReason("Authorization header not set").AsTask());
        }

        public static Task<IHttpResponse> GetSessionIdClaimsAsync(this IHttpRequest request,
            Func<Guid, System.Security.Claims.Claim[], Task<IHttpResponse>> success)
        {
            var sessionIdClaimTypeConfigurationSetting =
                EastFive.Api.AppSettings.SessionIdClaimType;
            return GetSessionIdClaimsAsync(request, sessionIdClaimTypeConfigurationSetting, success);
        }

        public static Task<IHttpResponse> GetSessionIdClaimsAsync(this IHttpRequest request,
            string sessionIdClaimTypeConfigurationSetting,
            Func<Guid, System.Security.Claims.Claim[], Task<IHttpResponse>> success)
        {
            var resultGetClaims = request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    //var accountIdClaimType =
                    //    ConfigurationManager.AppSettings[sessionIdClaimTypeConfigurationSetting];
                    var sessionIdClaimType =  Auth.ClaimEnableSessionAttribute.Type;
                    var result = claims.GetSessionIdAsync(
                        request, sessionIdClaimType,
                        (sessionId) => success(sessionId, claims));
                    return result;
                },
                () => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason("Authorization header not set").AsTask(),
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason(why).AsTask());
            return resultGetClaims;
        }
        
        public static IHttpResponse GetActorIdClaims(this IHttpRequest request,
            Func<Guid, System.Security.Claims.Claim[], IHttpResponse> success)
        {
            var accountIdClaimTypeConfigurationSetting =
                EastFive.Api.AppSettings.ActorIdClaimType;
            return GetActorIdClaims(request, accountIdClaimTypeConfigurationSetting, success);
        }

        public static IHttpResponse GetActorIdClaims(this IHttpRequest request,
            string accountIdClaimTypeConfigurationSetting,
            Func<Guid, System.Security.Claims.Claim[], IHttpResponse> success)
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

        public static Task<IHttpResponse> GetActorIdClaimsAsync(this IHttpRequest request,
            Func<Guid, System.Security.Claims.Claim[], Task<IHttpResponse>> success)
        {
            var resultGetClaims = request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    var accountIdClaimType = "http://schemas.xmlsoap.org/ws/2009/09/identity/claims/actor";
                    var result = claims.GetAccountIdAsync(
                        request, accountIdClaimType,
                        (accountId) => success(accountId, claims));
                    return result;
                },
                () => request
                    .CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason("Authorization header not set").AsTask(),
                (why) => request
                    .CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason(why).AsTask());
            return resultGetClaims;
        }


        public static Task<IHttpResponse> GetActorIdClaimsFromBearerParamAsync(this IHttpRequest request,
            Func<Guid, System.Security.Claims.Claim[], Task<IHttpResponse>> onSuccess,
            Func<Task<IHttpResponse>> onNoBearerParameterFound,
            Func<Task<IHttpResponse>> onAuthorizationNotSet,
            Func<Task<IHttpResponse>> onFailure)

        {
            var accountIdClaimTypeConfigurationSetting =
                EastFive.Api.AppSettings.ActorIdClaimType;

            if(!request.RequestUri.TryGetQueryParam("bearer", out string bearer))
                return onNoBearerParameterFound();

            var foundClaims = bearer.GetClaimsFromJwtToken(
                claims =>
                {
                    var accountIdClaimType =
                        ConfigurationManager.AppSettings[accountIdClaimTypeConfigurationSetting];
                    var result = claims.GetAccountIdAsync(
                        request, accountIdClaimType,
                        (accountId) => onSuccess(accountId, claims.ToArray()));
                    return result;
                },
                onAuthorizationNotSet,
                (why) => onFailure());

            return foundClaims;
        }

        public static Task<IHttpResponse[]> GetActorIdClaimsAsync(this IHttpRequest request,
            string accountIdClaimType,
            Func<Guid, System.Security.Claims.Claim[], Task<IHttpResponse[]>> success)
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
                    .AddReason("Authorization header not set").AsEnumerable().ToArray().AsTask(),
                (why) => request.CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason(why).AsEnumerable().ToArray().AsTask());
            return resultGetClaims;
        }
        
        #endregion
    }
}
