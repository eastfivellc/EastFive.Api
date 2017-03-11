using System;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Web.Http.Routing;

using BlackBarLabs.Web;
using EastFive.Api.Services;
using System.Linq;

namespace BlackBarLabs.Api
{
    [Obsolete("Use ToActionResult instead")]
    public class Resource
    {
        public void Configure(HttpRequestMessage request, UrlHelper url)
        {
            this.Request = request;
            this.Url = url;
        }

        [IgnoreDataMember]
        public HttpRequestMessage Request { get; set; }

        [IgnoreDataMember]
        protected UrlHelper Url { get; private set; }

        [Obsolete("Use ToActionResult instead")]
        private IEnumerable<System.Security.Claims.Claim> claimsContext;

        [IgnoreDataMember]
        protected IEnumerable<System.Security.Claims.Claim> Claims
        {
            get
            {
                if (null == Request) return new System.Security.Claims.Claim[] { };
                if (null == Request.Headers) return new System.Security.Claims.Claim[] { };
                return Request.Headers.Authorization.GetClaimsFromAuthorizationHeader(
                    claimsContext =>
                    {
                        return claimsContext.ToArray();
                    },
                    () =>
                    {
                        return new System.Security.Claims.Claim[] { };
                    },
                    (why) =>
                    {
                        return new System.Security.Claims.Claim[] { };
                    },
                    "BlackBarLabs.Security.SessionServer.issuer", "BlackBarLabs.Security.SessionServer.key");
            }
        }

        private ISendMessageService mailService;
        protected ISendMessageService MailService
        {
            get
            {
                if (default(ISendMessageService) == this.mailService)
                {
                    var getMailService = (Func<ISendMessageService>)
                        this.Request.Properties[ServicePropertyDefinitions.MailService];
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
                    if (!this.Request.Properties.ContainsKey(BlackBarLabs.Api.ServicePropertyDefinitions.TimeService))
                        return new TimeService();
                    var dateTimeService = (Func<ITimeService>)
                        this.Request.Properties[BlackBarLabs.Api.ServicePropertyDefinitions.TimeService];
                    this.dateTimeService = dateTimeService();
                }
                return this.dateTimeService;
            }
        }

    }
}
