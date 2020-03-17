using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [Security]
    public struct Security
    {
        public Guid performingAsActorId;
        public System.Security.Claims.Claim[] claims;
    }

    public class SecurityAttribute : Attribute, IInstigatable
    {
        public Task<HttpResponseMessage> Instigate(IApplication httpApp,
                HttpRequestMessage request, CancellationToken cancellationToken,
                ParameterInfo parameterInfo, 
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            return request
                .GetActorIdClaimsAsync(
                    (actorId, claims) =>
                    {
                        var security = new Security
                        {
                            performingAsActorId = actorId,
                            claims = claims,
                        };
                        return onSuccess(security);
                    });
        }
    }
}
