using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class FunctionViewController4Attribute : FunctionViewControllerAttribute, IProvideSerialization
    {
        public string MediaType => "application/json";

        public Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeConvert();
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                });
            var contentType = this.ContentType.HasBlackSpace() ?
                this.ContentType
                :
                this.MediaType;
            var streamWriter = request.TryGetAcceptEncoding(out Encoding writerEncoding) ?
                new StreamWriter(responseStream, writerEncoding)
                :
                new StreamWriter(responseStream, Encoding.UTF8);
            return streamWriter.WriteAsync(jsonObj);
        }
    }
}
