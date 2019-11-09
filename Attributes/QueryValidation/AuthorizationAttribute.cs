using BlackBarLabs.Api;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Web.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class AuthorizationAttribute : System.Attribute, IBindApiValue
    {
        public virtual SelectParameterResult TryCast(IApplication httpApp,
                HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
                Api.CastDelegate fetchQueryParam,
                Api.CastDelegate fetchBodyParam,
                Api.CastDelegate fetchDefaultParam)
        {
            return request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();
                    return AppSettings.ActorIdClaimType.ConfigurationString(
                        (accountIdClaimType) =>
                        {
                            return claims
                                .First<Claim, SelectParameterResult>(
                                    (claim, next) =>
                                    {
                                        if (String.Compare(claim.Type, accountIdClaimType) == 0)
                                        {
                                            var accountId = Guid.Parse(claim.Value);
                                            if (parameterRequiringValidation.ParameterType.IsSubClassOfGeneric(typeof(IRef<>)))
                                            {
                                                var instantiatableRefType = typeof(Ref<>)
                                                    .MakeGenericType(parameterRequiringValidation.ParameterType.GenericTypeArguments);
                                                var refInstance = Activator.CreateInstance(instantiatableRefType,
                                                    new object[] { accountId });
                                                return SelectParameterResult.Header(refInstance, "Authentication", parameterRequiringValidation);
                                            }
                                            return SelectParameterResult.FailureHeader(
                                                $"Inform server developer type `{parameterRequiringValidation.ParameterType.FullName}` is not a valid Authorization result.",
                                                "Authentication", parameterRequiringValidation);
                                        }
                                        return next();
                                    },
                                    () =>
                                    {
                                        return SelectParameterResult.FailureHeader("Account is not set in token",
                                            "Authentication", parameterRequiringValidation);
                                    });

                        },
                        (why) => SelectParameterResult.FailureHeader(why, "Authentication", parameterRequiringValidation));
                },
                () => SelectParameterResult.FailureHeader("Authentication header not set.",
                    "Authentication", parameterRequiringValidation),
                (why) => SelectParameterResult.FailureHeader(why, "Authentication", parameterRequiringValidation));
        }

        public virtual string GetKey(ParameterInfo paramInfo)
        {
            return "Authorization";
        }
    }
}
