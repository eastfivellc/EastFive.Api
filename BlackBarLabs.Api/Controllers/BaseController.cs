using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace OrderOwl.Api.Controllers
{
    public class BaseController : ApiController
    {
        protected BaseController()
        {
           
        }

        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);

            Func<BlackBarLabs.Web.ISendMailService> mailerServiceCreate =
                () =>
                {
                    var username = ConfigurationManager.AppSettings["SendGrid.UserName"];
                    var password  = ConfigurationManager.AppSettings["SendGrid.Password"];

                    string testToEmailRedirect = null;
                    var doEmailRedirect = ConfigurationManager.AppSettings["Test.ToEmail.DoRedirect"];
                    if (doEmailRedirect == "true")
                        testToEmailRedirect = ConfigurationManager.AppSettings["Test.ToEmail.Redirect"];

                    var mailer = new BlackBarLabs.SendGrid.Mailer(username, password, testToEmailRedirect);
                    return mailer;
                };

            controllerContext.Request.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.MailService,
                mailerServiceCreate);

            Func<DateTime> fetchDateTimeUtc =
                () => DateTime.UtcNow;
            controllerContext.Request.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.TimeService,
                fetchDateTimeUtc);
        }
    }
}