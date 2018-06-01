using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Controllers
{
    public struct Security
    {
        public Guid performingAsActorId;
        public System.Security.Claims.Claim[] claims;
    }
}
