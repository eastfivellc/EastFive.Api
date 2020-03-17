using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Routing;

using Newtonsoft.Json;

using EastFive.Linq;
using BlackBarLabs.Api;
using EastFive.Reflection;
using Newtonsoft.Json.Linq;

namespace EastFive.Api.Serialization
{
    public class ExtrudeDictionarySafeConvert : ExtrudeConvert
    {

        public ExtrudeDictionarySafeConvert(IApplication httpApplication, HttpRequestMessage request)
            : base(httpApplication, request)
        {
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var valueType = value.GetType();
            if (valueType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                writer.WriteStartArray();
                foreach (var kvpObj in value.DictionaryKeyValuePairs())
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Key");
                    var keyValue = kvpObj.Key;
                    serializer.Serialize(writer, keyValue);
                    // WriteJson(writer, keyValue, serializer);

                    writer.WritePropertyName("Value");
                    var valueValue = kvpObj.Value;
                    //var valueJsonStr = JsonConvert.SerializeObject(valueValue, this);
                    ////var valueJObj = JObject.FromObject(valueValue, serializer);
                    //var valueJObj = JToken.Parse(valueJsonStr);
                    //valueJObj.WriteTo(writer, this);
                    serializer.Serialize(writer, valueValue);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                return;
            }
            base.WriteJson(writer, value, serializer);
        }
    }
}
