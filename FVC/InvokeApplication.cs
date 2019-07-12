using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace EastFive.Api
{
    public abstract class InvokeApplication : IInvokeApplication
    {
        public virtual string[] ApiRoutes => new string[] { ApiRouteName };

        public virtual string[] MvcRoutes => new string[] { };

        public Uri ServerLocation { get; private set; }
        public string ApiRouteName { get; private set; }

        public IDictionary<string, string> Headers { get; private set; }

        public string AuthorizationHeader
        {
            get
            {
                if (this.Headers.IsDefaultOrNull())
                    return null;
                if (!this.Headers.ContainsKey("Authorization"))
                    return null;
                return this.Headers["Authorization"];
            }
            set
            {
                if (this.Headers.IsDefaultOrNull())
                    this.Headers = new Dictionary<string, string>();
                if (this.Headers.ContainsKey("Authorization"))
                {
                    this.Headers["Authorization"] = value;
                    return;
                }
                this.Headers.Add("Authorization", value);
            }
        }

        public InvokeApplication(Uri serverUrl, string apiRouteName)
        {
            this.ServerLocation = serverUrl;
            this.ApiRouteName = apiRouteName;
            this.Headers = new Dictionary<string, string>();
        }

        public abstract IApplication Application { get; }


        public virtual RequestMessage<TResource> GetRequest<TResource>()
        {
            var httpRequest = new HttpRequestMessage();
            var config = new HttpConfiguration();

            var updatedConfig = ConfigureRoutes(httpRequest, config);

            httpRequest.SetConfiguration(updatedConfig);

            foreach (var headerKVP in this.Headers)
                httpRequest.Headers.Add(headerKVP.Key, headerKVP.Value);

            httpRequest.RequestUri = this.ServerLocation;

            return BuildRequest<TResource>(this.Application, httpRequest);
        }

        protected virtual HttpConfiguration ConfigureRoutes(HttpRequestMessage httpRequest, HttpConfiguration config)
        {
            var apiRoutes = this.ApiRoutes
                .Select(
                    routeName =>
                    {
                        var route = config.Routes.MapHttpRoute(
                            name: routeName,
                            routeTemplate: routeName + "/{controller}/{id}",
                            defaults: new { id = RouteParameter.Optional }
                        );
                        httpRequest.SetRouteData(new System.Web.Http.Routing.HttpRouteData(route));
                        return route;
                    })
                .ToArray();

            var mvcRoutes = this.MvcRoutes
                .Select(
                    routeName =>
                    {
                        var route = config.Routes.MapHttpRoute(
                            name: routeName,
                            routeTemplate: "{controller}/{action}/{id}",
                            defaults: new { controller = "Default", action = "Index", id = "" }
                            );
                        httpRequest.SetRouteData(new System.Web.Http.Routing.HttpRouteData(route));
                        return route;
                    })
                .ToArray();
            return config;
        }

        protected virtual RequestMessage<TResource> BuildRequest<TResource>(IApplication application, HttpRequestMessage httpRequest)
        {
            return new RequestMessage<TResource>(this, httpRequest);
        }

        public abstract Task<HttpResponseMessage> SendAsync<TResource>(RequestMessage<TResource> requestMessage, HttpRequestMessage httpRequest);
    }

    
}
