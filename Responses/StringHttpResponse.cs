using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using EastFive.Extensions;

namespace EastFive.Api
{
    public class StringHttpResponse : HttpResponse
    {
        private string content;
        private Encoding encoding;

        public StringHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string fileName, string contentType, bool? inline,
            string content, Encoding encoding)
            : base(request, statusCode)
        {
            this.content = content;
            this.encoding = encoding;
            this.SetFileHeaders(fileName, contentType, inline);
        }

        public override Task WriteResponseAsync(Stream responseStream)
        {
            var streamWriter = encoding.IsDefaultOrNull() ?
                this.Request.TryGetAcceptEncoding(out Encoding writerEncoding) ?
                    new StreamWriter(responseStream, writerEncoding)
                    :
                    new StreamWriter(responseStream)
                :
                new StreamWriter(responseStream, encoding);
            return streamWriter.WriteAsync(content);
        }
    }
}
