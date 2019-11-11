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

using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Serialization;
using EastFive.Web.Configuration;

namespace EastFive.Api
{
    [AccessTokenAccount]
    public struct AccessTokenAccount
    {
        public Guid sessionId;
        public Guid accountId;
        public DateTime expirationUtc;
        public string token;
    }

    public class AccessTokenAccountAttribute : Attribute, IInstigatable, IBindApiParameter<string>
    {
        public TResult Bind<TResult>(Type type, string content,
            Func<object, TResult> onParsed, 
            Func<string, TResult> onDidNotBind, 
            Func<string, TResult> onBindingFailure)
        {
            return onParsed(default(AccessTokenAccount));
        }

        public Task<HttpResponseMessage> Instigate(HttpApplication httpApp,
                HttpRequestMessage request, CancellationToken cancellationToken, 
                ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            return request.RequestUri.ValidateAccessTokenAccount(
                accessToken => onSuccess(accessToken),
                () => request
                    .CreateResponse(HttpStatusCode.NotImplemented)
                    .AddReason($"Missing `{AccessTokenAccountExtensions.QueryParameter}` parameter")
                    .AsTask(),
                () => request
                    .CreateResponse(HttpStatusCode.BadRequest)
                    .AddReason($"Invalid `{AccessTokenAccountExtensions.QueryParameter}` parameter")
                    .AsTask(),
                () => request
                    .CreateResponse(HttpStatusCode.Unauthorized)
                    .AddReason("Access token has expired.")
                    .AsTask(),
                () => request
                    .CreateResponse(HttpStatusCode.Forbidden)
                    .AddReason($"Incorrect signature on `{AccessTokenAccountExtensions.QueryParameter}` parameter")
                    .AsTask(),
                () => request
                    .CreateResponse(HttpStatusCode.Unauthorized)
                    .AddReason("AccountAccessTokens are not configured for this system.")
                    .AsTask());
        }
    }

    public static class AccessTokenAccountExtensions
    {
        public const string QueryParameter = "access_token";

        private static DateTime GetEpoch() => new DateTime(2010, 1, 1);

        public static TResult SignWithAccessTokenAccount<TResult>(this Uri originalUrl,
                Guid sessionId, Guid accountId,
                DateTime expirationUtc,
            Func<Uri, TResult> onSuccess,
            Func<TResult> onSystemNotConfigured = default)
        {
            var expiresAfterEpoch = expirationUtc - GetEpoch();
            var secondsAfterEpoch = (uint)expiresAfterEpoch.TotalSeconds;

            return originalUrl.CreateSignature(sessionId, accountId, secondsAfterEpoch,
                (accessToken, signature) =>
                {
                    var accessTokenString = accessToken.ToBase64String();
                    var accessTokenUrl = originalUrl.AddQueryParameter(QueryParameter, accessTokenString);
                    return onSuccess(accessTokenUrl);
                },
                onSystemNotConfigured);
        }

        public static TResult ValidateAccessTokenAccount<TResult>(this Uri url,
            Func<AccessTokenAccount, TResult> onSuccess,
            Func<TResult> onAccessTokenNotProvided,
            Func<TResult> onAccessTokenInvalid,
            Func<TResult> onAccessTokenExpired = default,
            Func<TResult> onInvalidSignature = default,
            Func<TResult> onSystemNotConfigured = default)
        {
            if (!url.TryGetQueryParam(QueryParameter, out string accessTokenString))
                return onAccessTokenNotProvided();
            var originalUrl = url.RemoveQueryParameter(QueryParameter);

            var accessTokenBytes = accessTokenString.FromBase64String();
            if (accessTokenBytes.Length != 68)
                return onAccessTokenInvalid();

            var sessionId = new Guid(accessTokenBytes.Take(16).ToArray());
            var accountId = new Guid(accessTokenBytes.Skip(16).Take(16).ToArray());

            var secondsSinceEpoch = BitConverter.ToUInt32(accessTokenBytes, 32);
            var expiration = GetEpoch() + TimeSpan.FromSeconds(secondsSinceEpoch);
            if (DateTime.UtcNow > expiration)
            {
                if (onAccessTokenExpired.IsDefaultOrNull())
                    return onAccessTokenInvalid();
                return onAccessTokenExpired();
            }

            var signature = accessTokenBytes.Skip(36).ToArray();
            return VerifySignature(signature,
                sessionId, accountId,
                secondsSinceEpoch, originalUrl,
                () =>
                {
                    return onSuccess(
                        new AccessTokenAccount()
                        {
                            sessionId = sessionId,
                            accountId = accountId,
                            expirationUtc = expiration,
                            token = accessTokenString,
                        });
                },
                () =>
                {
                    if (onInvalidSignature.IsDefaultOrNull())
                        return onAccessTokenInvalid();

                    return onInvalidSignature();
                },
                onSystemNotConfigured);
        }

        public static TResult CreateSignature<TResult>(this Uri originalUrl,
                Guid sessionId, Guid accountId,
                uint secondsAfterEpoch, 
            Func<byte[], byte[], TResult> onAccessTokenAndEnvelope,
            Func<TResult> onNotConfigured = default)
        {
            return AppSettings.AccessTokenSecret.ConfigurationGuid(
                (apiSecret) =>
                {
                    var originalUrlStr = originalUrl.AbsoluteUri;
                    var urlHash = originalUrlStr.MD5HashGuid();

                    var securityEnthropy = sessionId.ToByteArray()
                        .Concat(accountId.ToByteArray())
                        .Concat(BitConverter.GetBytes(secondsAfterEpoch));
                    var dataToSign = securityEnthropy
                        .Concat(urlHash.ToByteArray());
                    var envelopeBytes = dataToSign
                        .Concat(apiSecret.ToByteArray())
                        .ToArray();
                    var envelopeSignature = envelopeBytes.SHA256Hash();
                    var accessToken = securityEnthropy
                        .Concat(envelopeSignature)
                        .ToArray();
                    return onAccessTokenAndEnvelope(accessToken, envelopeSignature);
                },
                (why) =>
                {
                    if (onNotConfigured.IsDefaultOrNull())
                        throw new ConfigurationException(AppSettings.ActorIdClaimType, typeof(Guid), why);

                    return onNotConfigured();
                });
        }

        public static TResult VerifySignature<TResult>(this byte[] signature,
                Guid sessionId, Guid accountId,
                uint secondsAfterEpoch, Uri originalUrl,
            Func<TResult> onIsValid,
            Func<TResult> onIsNotValid,
            Func<TResult> onNotConfigured = default)
        {
            return originalUrl.CreateSignature(sessionId, accountId, secondsAfterEpoch,
                (accessToken, envelopeSignature) =>
                {
                    if (envelopeSignature.SequenceEqual(signature))
                        return onIsValid();

                    return onIsNotValid();
                },
                onNotConfigured);
        }
    }
}
