using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Configuration
{
    public static class SecurityDefinitions
    {
        public const string ActorIdClaimType = "EastFive.Api.Security.AccountIdClaimType";
        public const string ActorIdSuperAdmin = "EastFive.Api.Security.SideAdminAccountId";
        public const string SiteAdminAuthorization = "EastFive.Api.Security.SiteAdminAuthorization";
    }
}
