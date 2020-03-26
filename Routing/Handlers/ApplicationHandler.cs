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

namespace EastFive.Api.Modules
{
    public abstract class ApplicationHandler : System.Net.Http.DelegatingHandler
    {
        protected IApplication application;
        private string applicationProperty = Guid.NewGuid().ToString("N");

        public ApplicationHandler(IApplication application)
        {
            this.application = application;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // In the event that SendAsync(HttpApplication ...) calls base.SendAsync(request, cancellationToken) then this method
            // would be called. This method would then in turn call back to SendAsync(HttpApplication...) which would cause 
            // recursion to stack overflow. Therefore, a property (.applicationProperty) is added to the request to identify if this method has
            // already been called.
            // This situation can be avoided by using the contiuation callback instead of calling base, this serves a defensive programming.

            // Check if this method has already been called
            if (request.Properties.ContainsKey(applicationProperty))
                // TODO: Log event here.
                return base.SendAsync(request, cancellationToken);
            
            // add applicationProperty as a property to identify this method has already been called.
            request.Properties.Add(applicationProperty, this.application);
            throw new NotImplementedException();
            //return SendAsync(this.application, request, cancellationToken,
            //    (requestBase, cancellationTokenBase) =>
            //        base.SendAsync(requestBase, cancellationTokenBase));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="httpApp"></param>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="continuation">In the event that the given application handler does not handle this request,
        /// this allows other handlers (SPA, Content, MVC, etc) to attempt to manage the request.</param>
        /// <returns></returns>
        protected abstract Task<IHttpResponse> SendAsync(IApplication httpApplication,
            IHttpRequest request, CancellationToken cancellationToken,
            Func<IHttpRequest, CancellationToken, Task<IHttpResponse>> continuation);
    }
}
