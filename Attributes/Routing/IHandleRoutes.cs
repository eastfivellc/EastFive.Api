using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<HttpResponseMessage> RouteHandlingDelegate(Type controllerType,
            IApplication httpApp, HttpRequestMessage request, string routeName);

    public interface IHandleRoutes
    {
        Task<HttpResponseMessage> HandleRouteAsync(Type controllerType,
            IApplication httpApp, HttpRequestMessage request, string routeName,
            RouteHandlingDelegate continueExecution);
    }
}
