using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EastFive.Extensions;
using Microsoft.AspNetCore.Http;

namespace EastFive.Api
{
    public class HttpResponse : IHttpResponse
    {
        public HttpResponse(IHttpRequest request, HttpStatusCode statusCode)
        {
            this.Request = request;
            this.StatusCode = statusCode;
            this.ReasonPhrase = string.Empty;
            this.Headers = new Dictionary<string, string[]>();
        }

        public IHttpRequest Request { get; private set; }

        public HttpStatusCode StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public IDictionary<string, string[]> Headers { get; private set; }

        public virtual Task WriteResponseAsync(System.IO.Stream responseStream)
        {
            return StatusCode.AsTask();
        }
    }
}
