using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class HttpRequest : IHttpRequest
    {
        public HttpRequest(Uri absoluteUri, CancellationToken cancellationToken = default)
        {
            this.RequestUri = absoluteUri;
            this.CancellationToken = cancellationToken;
        }

        public Uri RequestUri { get; set; }

        public CancellationToken CancellationToken { get; private set; }

        public HttpMethod Method { get; set; }
        
        public Func<Stream, Task> WriteBody { get; set; }

        public Stream Body => throw new NotImplementedException();

        public IFormCollection Form => throw new NotImplementedException();

        public IDictionary<string, string[]> Headers => throw new NotImplementedException();

        IDictionary<string, object> IHttpRequest.Properties { get; }

        public IEnumerable<string> GetHeaders(string headerKey)
        {
            throw new NotImplementedException();
        }

        public void UpdateHeader(string headerKey,
            Func<string[], string[]> callback)
        {
            throw new NotImplementedException();
        }
    }
}
