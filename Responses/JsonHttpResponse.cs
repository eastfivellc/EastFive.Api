using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using EastFive.Extensions;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

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
        }

        public async override Task WriteResultAsync(HttpContext context)
        {
            await base.WriteResultAsync(context);
            context.Response.Headers.Add("Content-Type", "application/json");
            var converter = new Serialization.ExtrudeConvert();
            var jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(content,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                });
            await context.Response.WriteAsync(jsonContent, Encoding.UTF8);
            return;
        }
    }
}
