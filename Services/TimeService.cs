using EastFive.Api.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;

namespace EastFive.Api.Services
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