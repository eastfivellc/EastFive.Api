using System;
using System.Net.Http;
using System.Runtime.Serialization;

namespace BlackBarLabs.Api
{
    public class Resource
    {
        [IgnoreDataMember]
        public HttpRequestMessage Request { protected get; set; }

        private BlackBarLabs.Web.ISendMailService mailService;
        protected BlackBarLabs.Web.ISendMailService MailService
        {
            get
            {
                if (default(BlackBarLabs.Web.ISendMailService) == this.mailService)
                {
                    var getMailService = (Func<BlackBarLabs.Web.ISendMailService>)
                        this.Request.Properties[BlackBarLabs.Api.ServicePropertyDefinitions.MailService];
                    this.mailService = getMailService();
                }
                return this.mailService;
            }
        }

        private Func<DateTime> fetchDateTimeUtc;
        protected Func<DateTime> FetchDateTimeUtc
        {
            get
            {
                if (default(Func<DateTime>) == this.fetchDateTimeUtc)
                {
                    var fetchDateTimeUtc = (Func<DateTime>)
                        this.Request.Properties[BlackBarLabs.Api.ServicePropertyDefinitions.TimeService];
                    this.fetchDateTimeUtc = fetchDateTimeUtc;
                }
                return this.fetchDateTimeUtc;
            }
        }
    }
}
