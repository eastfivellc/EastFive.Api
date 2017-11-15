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

        [Obsolete]
        public const string SiteAdminAuthorization = "EastFive.Api.Security.SiteAdminAuthorization";
    }
}
