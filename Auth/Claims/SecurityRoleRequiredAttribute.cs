using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api.Auth
{
    public class SecurityRoleRequiredAttribute : Attribute, IValidateHttpRequest
    {
        public virtual string [] RolesAllowed { get; set; }

        public virtual string[] RolesDenied { get; set; }

        public Task<IHttpResponse> ValidateRequest(
            KeyValuePair<ParameterInfo, object>[] parameterSelection,
            MethodInfo method,
            IApplication httpApp,
            IHttpRequest request,
            ValidateHttpDelegate boundCallback)
        {
            Func<string, bool> roleInterigator =
                request.Properties.TryGetValueNullSafe(Token.PropertyName, out object tokenObj) ?
                    (roll) => ((Token)tokenObj).IsAuthorizedFor(ClaimTypes.Role, roll)
                    :
                    (roll) => request.IsAuthorizedFor(new Uri(ClaimTypes.Role), roll);

            return ProcessClaimsAsync(roleInterigator);

            Task<IHttpResponse> ProcessClaimsAsync(Func<string, bool> doesContainRole)
            {
                return RolesDenied
                    .NullToEmpty()
                    .Where(rollAllowed => doesContainRole(rollAllowed))
                    .First(
                        (rollDenied, next) =>
                        {
                            return DenyAsync("denies", rollDenied);
                        },
                        () =>
                        {
                            if (!RolesAllowed.Any())
                                return boundCallback(parameterSelection, method, httpApp, request);

                            return RolesAllowed
                                .Where(rollAllowed => doesContainRole(rollAllowed))
                                .First(
                                    (rollAllowed, next) => boundCallback(parameterSelection, method, httpApp, request),
                                    () => DenyAsync("requires one of", RolesAllowed.Join(',')));
                        });
            }


            Task<IHttpResponse> DenyAsync(string action, string equals)
            {
                return request
                    .CreateResponse(System.Net.HttpStatusCode.Unauthorized)
                    .AddReason($"{method.DeclaringType.FullName}..{method.Name} {action} role claim ({ClaimTypes.Role}) = `{equals}`")
                    .AsTask();
            }
        }
    }
}
