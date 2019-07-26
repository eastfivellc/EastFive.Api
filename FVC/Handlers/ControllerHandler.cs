using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using EastFive.Serialization;
using System.Net.NetworkInformation;
using EastFive.Extensions;
using BlackBarLabs.Web;
using System.Reflection;
using System.Net.Http;
using EastFive.Linq;
using System.Net;
using BlackBarLabs.Api;
using BlackBarLabs;
using System.Threading;
using System.IO;
using EastFive.Linq.Async;
using EastFive.Api.Serialization;

namespace EastFive.Api.Modules
{
    public class ControllerHandler : ApplicationHandler
    {
        public ControllerHandler(System.Web.Http.HttpConfiguration config)
            : base(config)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpApplication httpApp, 
            HttpRequestMessage request, CancellationToken cancellationToken, 
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> continuation)
        {
            return DirectSendAsync(httpApp, request, cancellationToken, continuation);
        }

        /// <summary>
        /// This method is available if an external system or test needs to invoke the routing.
        /// SendAsync serves as the MVC.API Binding to this method.
        /// </summary>
        /// <param name="httpApp"></param>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="continuation"></param>
        /// <returns></returns>
        public static async Task<HttpResponseMessage> DirectSendAsync(IApplication httpApp,
            HttpRequestMessage request, CancellationToken cancellationToken,
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> continuation)
        {
            var path = request.RequestUri.Segments
                .Where(pathPart => !pathPart.IsNullOrWhiteSpace())
                .ToArray();

            if (path.Length < 3)
                return await continuation(request, cancellationToken);
            var routeName = path[2].ToLower().Trim('/'.AsArray());

            return await httpApp.GetControllerType(routeName,
                async (controllerType) =>
                {
                    var invokeResource = controllerType.GetAttributesInterface<IInvokeResource>().First();
                    return await invokeResource.CreateResponseAsync(controllerType, httpApp, request, routeName);
                },
                () => continuation(request, cancellationToken));
        }


    }
}
