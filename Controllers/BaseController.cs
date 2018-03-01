using System;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Api.Services;
using EastFive.Linq.Expressions;
using EastFive.Collections.Generic;
using BlackBarLabs.Extensions;

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
        }
    }
}
