using System;
using System.Net.Http;

using BlackBarLabs.Extensions;
using EastFive.Api.Services;

namespace BlackBarLabs.Api
{
    public static class RequestServiceExtensions
    {
        public static Func<ISendMessageService> GetMailService(this HttpRequestMessage request)
        {
            var mailService = default(ISendMessageService);
            return () =>
            {
                if (mailService.IsDefaultOrNull())
                {
                    var getMailService = (Func<ISendMessageService>)
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
                        dateTimeService = new TimeService();
                    else
                        dateTimeService = ((Func<ITimeService>)
                            request.Properties[ServicePropertyDefinitions.TimeService])();
                }
                return dateTimeService;
            };
        }
    }
}
