using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EastFive.Api
{
    public class HttpRequest : IHttpRequest
    {
        private Uri absoluteUri;

        public HttpRequest(Uri absoluteUri, CancellationToken cancellationToken = default)
        {
            this.absoluteUri = absoluteUri;
            this.CancellationToken = cancellationToken;
        }

        public Uri AbsoluteUri => absoluteUri;

        public CancellationToken CancellationToken { get; private set; }

        public IEnumerable<string> GetHeaders(string headerKey)
        {
            throw new NotImplementedException();
        }

        public void UpdateHeader(string headerKey, Func<string[], string[]> callback)
        {
            throw new NotImplementedException();
        }
    }
}
