using EastFive.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Configuration
{
    public static class SecurityDefinitions
    {
        [ConfigKey("The id of the claim that holds the session id", 
            DeploymentOverrides.Optional, 
            DeploymentSecurityConcern = false,
            Location = "This value can be found by examining a claims set for the claim with the session Id on it")]
        public const string SessionIdClaimType = "EastFive.Api.Security.SessionIdClaimType";
        public const string ActorIdClaimType = "EastFive.Api.Security.AccountIdClaimType";
        public const string ActorIdSuperAdmin = "EastFive.Api.Security.SiteAdminAccountId";
        public const string SiteAdminAuthorization = "EastFive.Api.Security.SiteAdminAuthorization";
    }
}
