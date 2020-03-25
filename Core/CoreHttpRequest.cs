using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Microsoft.AspNetCore.Http;

namespace EastFive.Api.Core
{
    public class CoreHttpRequest : IHttpRequest
    {
        public HttpRequest request;

        public CoreHttpRequest(HttpRequest request, CancellationToken cancellationToken)
        {
            this.request = request;
            this.CancellationToken = cancellationToken;
        }

        public IDictionary<string, object> Properties;

        public Uri AbsoluteUri => request.GetAbsoluteUri();

        public CancellationToken CancellationToken { get; private set; }

        public IEnumerable<string> GetHeaders(string headerKey)
            => request.GetHeaders(headerKey);

        public void UpdateHeader(string headerKey, Func<string[], string[]> callback)
        {
            throw new NotImplementedException();
        }
    }
}
