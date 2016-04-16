using System;
using System.Net.Http;
using System.Runtime.Serialization;
using BlackBarLabs.Web.Services;

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

        private ITimeService dateTimeService;
        protected ITimeService DateTimeService
        {
            get
            {
                if (default(ITimeService) == this.dateTimeService)
                {
                    var dateTimeService = (Func<ITimeService>)
                        this.Request.Properties[BlackBarLabs.Api.ServicePropertyDefinitions.TimeService];
                    this.dateTimeService = dateTimeService();
                }
                return this.dateTimeService;
            }
        }

    }
}
