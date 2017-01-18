using System;
using System.Web.Http;
using System.Web.Http.Controllers;

using EastFive.Api.Services;

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

            Func<ITimeService> fetchDateTimeUtc =
                () => new TimeService();
            controllerContext.Request.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.TimeService,
                fetchDateTimeUtc);
        }
    }
}