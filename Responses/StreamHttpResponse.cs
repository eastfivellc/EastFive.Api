using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class StreamHttpResponse : EastFive.Api.HttpResponse
    {
        private Stream stream;

        public StreamHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string fileName, string contentType, bool? inline,
            Stream stream)
            : base(request, statusCode)
        {
            this.SetFileHeaders(fileName, contentType, inline);
        }

        public override Task WriteResponseAsync(Stream responseStream)
        {
            return stream.CopyToAsync(responseStream);
        }
    }
}
