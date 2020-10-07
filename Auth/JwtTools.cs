using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Auth
{
    public static class JwtTools
    {
        public static TResult CreateToken<TResult>(Guid sessionId,
                Uri scope, TimeSpan duration, IDictionary<string, string> claims,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey)
        {
            var claimsAuth = (IEnumerable<Claim>)new[]
            {
                new Claim(ClaimEnableSessionAttribute.Type, sessionId.ToString()),
            };
            var claimsCrypt = claims.NullToEmpty()
                .Select(kvp => new Claim(kvp.Key, kvp.Value));

            var issued = DateTime.UtcNow;
            var result = EastFive.Security.Tokens.JwtTools.CreateToken(scope,
                issued, duration, claimsAuth.Concat(claimsCrypt),
                tokenCreated, missingConfigurationSetting, invalidConfigurationSetting,
                configNameOfIssuer, configNameOfRSAKey);
            return result;
        }

        public static TResult CreateToken<TResult>(Guid sessionId, Uri scope,
            TimeSpan duration,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey)
        {
            return CreateToken(sessionId, scope, duration, default(IDictionary<string, string>),
                tokenCreated, missingConfigurationSetting, invalidConfigurationSetting,
                configNameOfIssuer, configNameOfRSAKey);
        }

        public static TResult CreateToken<TResult>(Guid sessionId, Guid authId, Uri scope,
            TimeSpan duration,
            IEnumerable<Claim> claims,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey)
        {
            var claimsAuth = new[] {
                new Claim(ClaimEnableSessionAttribute.Type, sessionId.ToString()),
                new Claim(ClaimEnableActorAttribute.Type, authId.ToString()) };
            var claimsCrypt = claims.NullToEmpty();

            var issued = DateTime.UtcNow;
            var result = EastFive.Security.Tokens.JwtTools.CreateToken(scope,
                    issued, duration, claimsAuth.Concat(claimsCrypt),
                tokenCreated, 
                missingConfigurationSetting, 
                invalidConfigurationSetting,
                    configNameOfIssuer, configNameOfRSAKey);
            return result;
        }

        public static TResult CreateToken<TResult>(Guid sessionId, Guid authId, Uri scope,
                TimeSpan duration,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
                string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
                string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey)
        {
            return CreateToken(sessionId, authId, scope, duration, default(IDictionary<string, string>),
                tokenCreated, missingConfigurationSetting, invalidConfigurationSetting,
                configNameOfIssuer, configNameOfRSAKey);
        }

        public static TResult CreateToken<TResult>(Guid sessionId, Guid authId, Uri scope,
            TimeSpan duration,
            IDictionary<string, string> claims,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey)
        {
            var claimsCrypt = claims.NullToEmpty().Select(kvp => new Claim(kvp.Key, kvp.Value));
            return CreateToken(sessionId, authId, scope, duration, claimsCrypt, tokenCreated, missingConfigurationSetting, invalidConfigurationSetting,
                configNameOfIssuer, configNameOfRSAKey);
        }
    }
}
