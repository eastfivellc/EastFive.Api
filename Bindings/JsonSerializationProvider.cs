using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class JsonSerializationProviderAttribute : Attribute, IProvideSerialization
    {
        public string MediaType => "application/json";

        public string ContentType => MediaType;

        public virtual Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeConvert();
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                });
            var streamWriter = request.TryGetAcceptEncoding(out Encoding writerEncoding) ?
                new StreamWriter(responseStream, writerEncoding)
                :
                new StreamWriter(responseStream, Encoding.UTF8);
            return streamWriter.WriteAsync(jsonObj);
        }
    }

    public class JsonSerializationDictionarySafeProviderAttribute : JsonSerializationProviderAttribute
    {
        public override Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeDictionarySafeConvert();
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                });
            var streamWriter = request.TryGetAcceptEncoding(out Encoding writerEncoding) ?
                new StreamWriter(responseStream, writerEncoding)
                :
                new StreamWriter(responseStream, Encoding.UTF8);
            return streamWriter.WriteAsync(jsonObj);
        }
    }
}
