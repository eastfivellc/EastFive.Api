using System;
using System.Net.Http;

using BlackBarLabs.Web;
using BlackBarLabs.Web.Services;
using BlackBarLabs.Extensions;

namespace BlackBarLabs.Api
{
    public static class RequestServiceExtensions
    {
        public static Func<ISendMailService> GetMailService(this HttpRequestMessage request)
        {
            var mailService = default(ISendMailService);
            return () =>
            {
                if (mailService.IsDefaultOrNull())
                {
                    var getMailService = (Func<ISendMailService>)
                        request.Properties[ServicePropertyDefinitions.MailService];
                    mailService = getMailService();
                }
                return mailService;
            };
        }

        public static Func<ITimeService> GetDateTimeService(this HttpRequestMessage request)
        {
            var dateTimeService = default(ITimeService);
            return () =>
            {
                if (dateTimeService.IsDefaultOrNull())
                {
                    if (!request.Properties.ContainsKey(ServicePropertyDefinitions.TimeService))
                        dateTimeService = new Services.TimeService();
                    else
                        dateTimeService = ((Func<ITimeService>)
                            request.Properties[ServicePropertyDefinitions.TimeService])();
                }
                return dateTimeService;
            };
        }
    }
}
