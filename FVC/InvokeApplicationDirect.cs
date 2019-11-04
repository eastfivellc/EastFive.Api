using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

using EastFive.Api.Modules;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api
{
    [InvokeApplicationDirect.Instigate]
    public class InvokeApplicationDirect : InvokeApplication
    {
        public InvokeApplicationDirect(IApplication application, Uri serverUrl, string apiRouteName) 
            : base(serverUrl, apiRouteName)
        {
            this.application = application;
        }

        private IApplication application;
        public override IApplication Application => application;

        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequest)
        {
            return ControllerHandler.DirectSendAsync(application, httpRequest, 
                default(CancellationToken),
                (requestBack, token) =>
                {
                    throw new Exception();
                });
        }

        public class InstigateAttribute : Attribute, IInstigatable
        {
            public Task<HttpResponseMessage> Instigate(HttpApplication httpApp, 
                    HttpRequestMessage request, ParameterInfo parameterInfo,
                Func<object, Task<HttpResponseMessage>> onSuccess)
            {
                string GetApiPrefix()
                {
                    try
                    {
                        return request.RequestUri.AbsolutePath.Trim('/'.AsArray()).Split('/'.AsArray()).First();
                    }
                    catch (Exception)
                    {

                    }
                    var routeData = request.GetRouteData();
                    if (routeData.IsDefaultOrNull())
                        return "api";
                    var route = routeData.Route;
                    if (route.IsDefaultOrNull())
                        return "api";
                    var routeTemplate = route.RouteTemplate;
                    if (routeTemplate.IsNullOrWhiteSpace())
                        return "api";
                    var directories = routeTemplate.Split('/'.AsArray());
                    if (!directories.AnyNullSafe())
                        return "api";
                    return directories.First();
                }
                var instance = new InvokeApplicationDirect(httpApp, request.RequestUri, GetApiPrefix());
                return onSuccess(instance);
            }
        }

    }

    
}
