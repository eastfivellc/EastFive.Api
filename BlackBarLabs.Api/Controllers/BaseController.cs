using System;
using System.Web.Http;
using System.Web.Http.Controllers;
using BlackBarLabs.Api.Services;
using BlackBarLabs.Web.Services;

namespace BlackBarLabs.Api.Controllers
{
    public class BaseController : ApiController
    {
        protected BaseController()
        {
           
        }

        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);

            Func<DateTime> fetchDateTimeUtc =
                () => DateTime.UtcNow;
            controllerContext.Request.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.TimeService,
                fetchDateTimeUtc);

            Func<IIdentityService> identityServiceCreate =
                () =>
                {
                    return new IdentityService(this.User.Identity);
                };
            controllerContext.Request.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService,
                identityServiceCreate);
        }
    }
}