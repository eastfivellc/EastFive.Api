using EastFive.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            this.Headers = new Dictionary<string, string[]>();
        }

        public Uri RequestUri { get; set; }

        public CancellationToken CancellationToken { get; private set; }

        public HttpMethod Method { get; set; }
        
        public Func<Stream, Task> WriteBody { get; set; }

        public bool HasBody { get; private set; }

        private Stream body;
        public Stream Body
        {
            get
            {
                return body;
            }
            set
            {
                HasBody = true;
                body = value;
            }
        }

        public bool HasFormContentType => false; // TODO:

        public IFormCollection Form => throw new NotImplementedException();

        public IDictionary<string, string[]> Headers { get; private set; }

        public IRazorViewEngine RazorViewEngine => throw new NotImplementedException();

        IDictionary<string, object> IHttpRequest.Properties { get; }

        public string GetHeader(string headerKey)
        {
            if (Headers.ContainsKey(headerKey))
            {
                var values = Headers[headerKey];
                return values.AnyNullSafe() ? values[0] : string.Empty;
            }
            return String.Empty;
        }

        public IRequestHeaders RequestHeaders => throw new NotImplementedException();

        public IEnumerable<string> GetHeaders(string headerKey)
        {
            if (Headers.ContainsKey(headerKey))
                return Headers[headerKey];
            return Enumerable.Empty<string>();
        }

        public void UpdateHeader(string headerKey,
            Func<string[], string[]> callback)
        {
            var currentHeaders = GetHeaders(headerKey).ToArray();
            var updatedHeaders = callback(currentHeaders);
            if (Headers.ContainsKey(headerKey))
            {
                Headers[headerKey] = updatedHeaders;
                return;
            }
            Headers.Add(headerKey, updatedHeaders);
        }

        public TResult ReadCookie<TResult>(string cookieKey, Func<string, TResult> onCookie, Func<TResult> onNotAvailable)
        {
            throw new NotImplementedException();
            //request.Headers
            //    .GetCookies()
            //    .NullToEmpty()
            //       .SelectMany(cookieBucket => cookieBucket.Cookies
            //           .Select(cookie => (cookie, cookieBucket.Expires)))
            //       .Where(cookie => cookie.cookie.Name == cookieKey)
            //       .First(
            //    )
        }
    }
}
