using System;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Web.Http.Routing;

using BlackBarLabs.Web;
using BlackBarLabs.Web.Services;

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
                if (null == Request) yield break;
                if (null == Request.Headers) yield break;
                claimsContext = Request.Headers.Authorization.GetClaimsFromAuthorizationHeader(
                    (claims) => claims,
                    () => null,
                    (why) => null);
                if (claimsContext != null)
                {
                    foreach (var claim in claimsContext)
                        yield return claim;
                }
            }
        }

        private ISendMailService mailService;
        protected ISendMailService MailService
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
                    if (!this.Request.Properties.ContainsKey(BlackBarLabs.Api.ServicePropertyDefinitions.TimeService))
                        return new BlackBarLabs.Api.Services.TimeService();
                    var dateTimeService = (Func<ITimeService>)
                        this.Request.Properties[BlackBarLabs.Api.ServicePropertyDefinitions.TimeService];
                    this.dateTimeService = dateTimeService();
                }
                return this.dateTimeService;
            }
        }

    }
}
