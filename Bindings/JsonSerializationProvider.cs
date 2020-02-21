using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

        public virtual HttpResponseMessage Serialize(HttpResponseMessage response,
            HttpApplication httpApp, HttpRequestMessage request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeConvert(httpApp, request);
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                });
            response.Content = new StringContent(jsonObj, Encoding.UTF8, ContentType);
            return response;
        }
    }

    public class JsonSerializationDictionarySafeProviderAttribute : JsonSerializationProviderAttribute
    {
        public override HttpResponseMessage Serialize(HttpResponseMessage response,
            HttpApplication httpApp, HttpRequestMessage request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeDictionarySafeConvert(httpApp, request);
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                });
            response.Content = new StringContent(jsonObj, Encoding.UTF8, ContentType);
            return response;
        }
    }
}
