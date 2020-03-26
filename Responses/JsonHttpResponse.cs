using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json;

using EastFive.Extensions;
using System.IO;

namespace EastFive.Api
{
    public class JsonHttpResponse<T> : HttpResponse
    {
        private T content;

        public JsonHttpResponse(IHttpRequest request,
            HttpStatusCode statusCode, T content) 
            : base(request, statusCode)
        {
            this.content = content;
            this.Headers.Add("Content-Type", "application/json".AsArray());
        }

        public override Task WriteResponseAsync(System.IO.Stream responseStream)
        {
            return WriteResponseAsync(responseStream, this.content, this.Request);
        }

        public static Task WriteResponseAsync(System.IO.Stream responseStream, T content,
            IHttpRequest request)
        {
            if (request.TryGetAcceptEncoding(out Encoding encoding))
                return WriteResponseAsync(responseStream, content, encoding);

            return WriteResponseAsync(responseStream, content);
        }

        public async static Task WriteResponseAsync(System.IO.Stream responseStream, object content,
            Encoding encoding = default)
        {
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new Serialization.Converter());
            settings.DefaultValueHandling = DefaultValueHandling.Ignore;
            var contentJsonString = JsonConvert.SerializeObject(content, settings);
            var writer = encoding.IsDefaultOrNull() ?
                new StreamWriter(responseStream)
                :
                new StreamWriter(responseStream, encoding);
            await writer.WriteAsync(contentJsonString);
        }
    }
}
