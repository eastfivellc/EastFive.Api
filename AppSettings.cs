using EastFive.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [Config]
    public static class AppSettings
    {
        [ConfigKey("The UUID of the site administration account", DeploymentOverrides.Suggested,
            Location = "This value can be any generated GUID",
            MoreInfo = "When a token is parsed, the authenticationId and claims[] are returned." + 
                "This presents a problem when the SiteAdminToken is provided since it does not contain an authenticationId." +
                "This configuration value is provided when the SiteAdminToken is used. Also, unless this behavior is overwritten," + 
                "This value is also used to identify users that are logged in as site admin",
            DeploymentSecurityConcern = true)]
        public const string ActorIdSuperAdmin = "EastFive.Api.Security.SiteAdminAccountId";
        
        [ConfigKey("The name of the claim that holds the session id",
            DeploymentOverrides.Optional,
            DeploymentSecurityConcern = false,
            Location = "This value can be found by examining a claims set for the claim with the session Id on it")]
        public const string SessionIdClaimType = "EastFive.Api.Security.SessionIdClaimType";
        
        [ConfigKey("The name of the claim that holds the authorization id",
            DeploymentOverrides.Optional,
            DeploymentSecurityConcern = false,
            Location = "This value can be found by examining a claims set for the claim with the authoriztion Id on it")]
        public const string ActorIdClaimType = "EastFive.Api.Security.AccountIdClaimType";
        
        public const string SiteAdminAuthorization = "EastFive.Api.Security.SiteAdminAuthorization";

        [ConfigKey("A secret key value that must be provided to access an endpoint with the ApiSecurity attribute set.",
            DeploymentOverrides.Optional,
            DeploymentSecurityConcern = true,
            Location = "This value is created from secure rand entropy")]
        public const string ApiKey = "EastFive.Api.ApiSecurityKey";


        [ConfigKey("URL of the server.",
            DeploymentOverrides.Optional,
            DeploymentSecurityConcern = false,
            Location = "This is the server's server location, i.e. `http://example.com/mysite/`")]
        public const string SiteUrl = "EastFive.Api.SiteUrl";

        [ConfigKey("Secret GUID that can be used to make any session a admin session.",
            DeploymentOverrides.Mandatory,
            DeploymentSecurityConcern = true,
            Location = "Should be dervied from secure random numbers into a GUID.")]
        public const string AuthorizationIdSuperAdmin = "EastFive.Api.AuthorizationIdSuperAdmin";

        
    }
}
