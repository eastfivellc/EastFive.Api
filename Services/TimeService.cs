using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using BlackBarLabs.Web.Services;

namespace BlackBarLabs.Api.Services
{
    public class TimeService : ITimeService
    {
        public DateTime Utc
        {
            get
            {
                return DateTime.UtcNow;
            }
        }
    }
}