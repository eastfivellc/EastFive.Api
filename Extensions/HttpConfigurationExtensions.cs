using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using EastFive;

namespace BlackBarLabs.Api
{
    public static class HttpConfigurationExtensions
    {
        public static void AddExternalControllers<TController>(this HttpConfiguration config)
            where TController : ApiController
        {
            typeof(TController)
                .GetCustomAttributes<RoutePrefixAttribute>()
                .Select(routePrefix => routePrefix.Prefix)
                .Distinct()
                .Select(routePrefix =>
                    config.Routes.MapHttpRoute(
                        name: routePrefix,
                        routeTemplate: routePrefix + "/{controller}/{id}",
                        defaults: new { id = RouteParameter.Optional }));

            var assemblyRecognition = new InjectableAssemblyResolver(typeof(TController).Assembly,
                config.Services.GetAssembliesResolver());
            config.Services.Replace(typeof(System.Web.Http.Dispatcher.IAssembliesResolver), assemblyRecognition);
        }
    }
}
