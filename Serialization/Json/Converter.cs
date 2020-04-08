using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Linq.Async;
using EastFive.Reflection;
using Newtonsoft.Json;

namespace EastFive.Api.Serialization
{
    public class Converter : EastFive.Serialization.Json.Converter
    {
        private IHttpRequest request;

        public Converter(IHttpRequest request)
        {
            this.request = request;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Type)
            {
                var typeValue = (value as Type);
                var serializationAttrs = typeValue
                    .GetAttributesInterface<IProvideSerialization>()
                    .Where(x => x.ContentType.ToLower().Contains("json"));
                if (serializationAttrs.Any())
                {
                    var serializationAttr = serializationAttrs
                        .OrderByDescending(x => x.GetPreference(this.request))
                        .First();
                    writer.WriteValue(serializationAttr.ContentType);
                    return;
                }
            }
            base.WriteJson(writer, value, serializer);
        }
    }

}
