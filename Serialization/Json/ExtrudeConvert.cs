using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Routing;

using Newtonsoft.Json;

using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Extensions;

namespace EastFive.Api.Serialization
{
    public class ExtrudeConvert : Newtonsoft.Json.JsonConverter
    {
        IHttpRequest request;
        IConvertJson[] jsonConverters;

        public ExtrudeConvert(IHttpRequest request, IApplication application)
        {
            this.request = request;
            this.jsonConverters = application.GetType()
                .GetAttributesInterface<IConvertJson>()
                .ToArray();
        }

        public override bool CanConvert(Type objectType)
        {
            if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                var shouldConvertDict = objectType
                    .GetGenericArguments()
                    .Any(ShouldConvertDictionaryType);
                if (shouldConvertDict)
                    return true;
            }
            if (objectType.IsSubclassOf(typeof(Type)))
                return true;
            if (objectType.IsEnum)
                return true;
            return jsonConverters.Any(jc => jc.CanConvert(objectType));
        }

        protected bool ShouldConvertDictionaryType(Type arg)
        {
            if (CanConvert(arg))
                return true;
            if (arg.ContainsCustomAttribute<IProvideSerialization>(true))
                return true;
            if (arg == typeof(object))
                return true;
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Extruder does not read values");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var converters = jsonConverters
                .Where(jc => jc.CanConvert(value.GetType()));
            if(converters.Any())
            {
                converters.First().Write(writer, value, serializer);
                return;
            }
               

            void WriteId(Guid? idMaybe)
            {
                if (!idMaybe.HasValue)
                {
                    writer.WriteValue((string)null);
                    return;
                }

                var id = idMaybe.Value;
                writer.WriteValue(id);
            }

            if (value is IReferenceable)
            {
                var id = (value as IReferenceable).id;
                WriteId(id);
                //writer.WriteValue(id);
                return;
            }

            if (value is IReferences)
            {
                writer.WriteStartArray();
                Guid[] ids = (value as IReferences).ids
                    .Select(
                        id =>
                        {
                            WriteId(id);
                                //writer.WriteValue(id);
                                return id;
                        })
                    .ToArray();
                writer.WriteEndArray();
                return;
            }

            if (value is IReferenceableOptional)
            {
                var id = (value as IReferenceableOptional).id;
                WriteId(id);
                //writer.WriteValue(id);
                return;
            }

            if(!value.TryGetType(out Type valueType))
            {
                writer.WriteNull();
                return;
            }

            if (valueType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                writer.WriteStartObject();
                foreach (var kvpObj in value.DictionaryKeyValuePairs())
                {
                    var keyValue = kvpObj.Key;
                    var propertyName = (keyValue is IReferenceable) ?
                        (keyValue as IReferenceable).id.ToString()
                        :
                        keyValue.ToString();
                    writer.WritePropertyName(propertyName);

                    var valueValue = kvpObj.Value;
                    var valueValueType = valueType.GenericTypeArguments.Last();
                    if (this.ShouldConvertDictionaryType(valueValueType))
                    {
                        WriteJson(writer, valueValue, serializer);
                        continue;
                    }
                    writer.WriteValue(valueValue);
                }
                writer.WriteEndObject();
                return;
            }

            if (value is Type)
            {
                var typeValue = (value as Type);
                var serializationAttrs = typeValue
                    .GetAttributesInterface<IProvideSerialization>();
                if(serializationAttrs.Any())
                {
                    var serializationAttr = serializationAttrs
                            .OrderByDescending(x => x.GetPreference(request))
                            .First();
                    writer.WriteValue(serializationAttr.ContentType);
                    return;
                }
                var stringType = typeValue.GetClrString();
                writer.WriteValue(stringType);
                return;
            }

            if(valueType.IsEnum)
            {
                var stringValue = Enum.GetName(valueType, value);
                writer.WriteValue(stringValue);
                return;
            }

            serializer.Serialize(writer, value);
        }
    }
}
