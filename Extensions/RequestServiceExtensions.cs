using System;
using System.Net.Http;
using BlackBarLabs.Web;
using BlackBarLabs.Extensions;
using EastFive.Api.Services;
using System.Threading.Tasks;
using EastFive.Web.Services;

namespace EastFive.Api
{
    public static class RequestServiceExtensions
    {
        public static Func<ISendMessageService> GetMailService(this HttpRequestMessage request)
        {
            return Web.Services.ServiceConfiguration.SendMessageService;
        }

        public static Func<ITimeService> GetDateTimeService(this HttpRequestMessage request)
        {
            return Web.Services.ServiceConfiguration.TimeService;
        }
    }
}
