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
using EastFive.Linq;
using Microsoft.ApplicationInsights.DataContracts;

namespace EastFive.Api
{
    [IInvokeApplication]
    public interface IInvokeApplication
    {
        Uri ServerLocation { get; }

        string ApiRouteName { get; }

        IDictionary<string, string> Headers { get; }

        IApplication Application { get; }

        RequestMessage<TResource> GetRequest<TResource>();

        Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequest);
    }

    public class IInvokeApplicationAttribute : Attribute, IInstigatable
    {
        public Task<HttpResponseMessage> Instigate(HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
                RequestTelemetry telemetry,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            var instance = Instigate(httpApp, request);
            return onSuccess(instance);
        }

        public static IInvokeApplication Instigate(HttpApplication httpApp, HttpRequestMessage request)
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
            Uri GetServerLocation()
            {
                if (request.RequestUri.IsDefaultOrNull())
                    return new Uri("http://example.com");

                var hostUrlString = request.RequestUri.GetLeftPart(UriPartial.Authority);
                return new Uri(hostUrlString);
            }
            var apiPrefix = GetApiPrefix();
            var serverLocation = GetServerLocation();
            var instance = new InvokeApplicationFromRequest(httpApp, request, serverLocation, apiPrefix);
            return instance;
        }

        private class InvokeApplicationFromRequest : InvokeApplication
        {
            private HttpApplication httpApp;
            private HttpRequestMessage request;

            //private string[] apiRoute;

            public override IApplication Application => httpApp;

            public InvokeApplicationFromRequest(HttpApplication httpApp, HttpRequestMessage request,
                    Uri serverLocation, string apiPrefix) : base(serverLocation, apiPrefix)
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
                return new RequestMessage<TResource>(this, httpRequest);
            }

            public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequest)
            {
                using (var client = new HttpClient())
                {
                    var response = await client.SendAsync(request);
                    return response;
                }
            }
        }
    }
}
