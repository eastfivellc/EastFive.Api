using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace BlackBarLabs.Api
{
    public class HttpActionResult : IHttpActionResult
    {
        private Func<Task<HttpResponseMessage>> callback;

        public HttpActionResult(Func<Task<HttpResponseMessage>> callback)
        {
            this.callback = callback;
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            return callback();
        }
    }
}
