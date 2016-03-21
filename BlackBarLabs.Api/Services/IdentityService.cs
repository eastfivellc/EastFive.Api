using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using BlackBarLabs.Web.Services;

namespace BlackBarLabs.Api.Services
{
    public class IdentityService : IIdentityService
    {
        private IIdentity identity;
        private readonly ClaimsIdentity claimsIdentity;

        public IdentityService(IIdentity identity)
        {
            this.identity = identity;
            claimsIdentity = (ClaimsIdentity) identity;
        }

        public Claim GetClaim(string type)
        {
            return claimsIdentity.Claims.FirstOrDefault(claim => claim.Type == type);
        }
    }
}