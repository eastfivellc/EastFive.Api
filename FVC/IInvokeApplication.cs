using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using EastFive;
using EastFive.Extensions;

namespace EastFive.Api
{
    [IInvokeApplication]
    public interface IInvokeApplication
    {
        Uri ServerLocation { get; }

        IDictionary<string, string> Headers { get; }

        RequestMessage<TResource> GetRequest<TResource>();
    }

    public class IInvokeApplicationAttribute : Attribute, IInstigatable
    {
        public Task<HttpResponseMessage> Instigate(HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo, 
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            var instance = new InvokeApplicationFromRequest(httpApp, request);
            return onSuccess(instance);
        }

        private class InvokeApplicationFromRequest : InvokeApplication
        {
            private HttpApplication httpApp;
            private HttpRequestMessage request;

            private string[] apiRoute;

            protected override IApplication Application => httpApp;

            public InvokeApplicationFromRequest(HttpApplication httpApp, HttpRequestMessage request) : base(request.RequestUri)
            {
                this.httpApp = httpApp;
                this.request = request;

                //var routes = request.GetConfiguration().Routes;

                //this.apiRoute = routes
                //    .Where(route => route.RouteTemplate.Contains("{action}"))
                //    .Select(
                //        route =>
                //        {
                //            return route.RouteTemplate;
                //        })
                //    .ToArray();
            }

            protected override HttpConfiguration ConfigureRoutes(HttpRequestMessage httpRequest, HttpConfiguration config)
            {
                httpApp.DefaultApiRoute(config);
                return config;
            }

            protected override RequestMessage<TResource> BuildRequest<TResource>(IApplication application, HttpRequestMessage httpRequest)
            {
                return new RequestMessage<TResource>(application, httpRequest);
            }
        }
    }
}
