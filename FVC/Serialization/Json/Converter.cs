using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EastFive.Linq.Async;
using EastFive.Reflection;
using Newtonsoft.Json;

namespace EastFive.Api.Serialization
{
    public class Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
                return true;
            if (objectType.IsSubclassOf(typeof(Type)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefObj<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                return true;
            // THis doesn't work because it will serialize the whole object as a single GUID
            //if (objectType.IsSubClassOfGeneric(typeof(IReferenceable)))
            //    return true;
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Guid GetGuid()
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var guidString = reader.Value as string;
                    return Guid.Parse(guidString);
                }
                throw new Exception();
            }

            Guid? GetGuidMaybe()
            {
                if (reader.TokenType == JsonToken.Null)
                    return default(Guid?);
                return GetGuid();
            }

            Guid[] GetGuids()
            {
                if (reader.TokenType == JsonToken.Null)
                    return new Guid[] { };

                IEnumerable<Guid> Enumerate()
                {
                    while (reader.TokenType != JsonToken.EndArray)
                    {
                        if (!reader.Read())
                            yield break;
                        var guidStr = reader.ReadAsString();
                        yield return Guid.Parse(guidStr);
                    }
                }
                return Enumerate().ToArray();
            }

            if (objectType.IsSubClassOfGeneric(typeof(IReferenceable)))
            {
                
                if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
                {
                    var id = GetGuid();
                    var refType = typeof(Ref<>).MakeGenericType(objectType.GenericTypeArguments);
                    return Activator.CreateInstance(refType, id);
                }
            }

            if (objectType.IsSubClassOfGeneric(typeof(IReferenceableOptional)))
            {
                if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                {
                    var id = GetGuidMaybe();
                    var refType = typeof(RefOptional<>).MakeGenericType(objectType.GenericTypeArguments);
                    return Activator.CreateInstance(refType, id);
                }
            }

            if (objectType.IsSubClassOfGeneric(typeof(IReferences)))
            {
                if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                {
                    var ids = GetGuids();
                    var refType = typeof(Refs<>).MakeGenericType(objectType.GenericTypeArguments);
                    return Activator.CreateInstance(refType, ids);
                }
            }
            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // THis doesn't work because it will serialize the whole object as a single GUID if (value is IReferenceable)
            if(value.GetType().IsSubClassOfGeneric(typeof(IRef<>)))
            {
                var id = (value as IReferenceable).id;
                writer.WriteValue(id);
            }
            if (value.GetType().IsSubClassOfGeneric(typeof(IRefObj<>)))
            {
                var id = (value as IReferenceable).id;
                writer.WriteValue(id);
            }
            if (value is IReferences)
            {
                writer.WriteStartArray();
                Guid[] ids = (value as IReferences).ids
                    .Select(
                        id =>
                        {
                            writer.WriteValue(id);
                            return id;
                        })
                    .ToArray();
                writer.WriteEndArray();
            }
            if (value is IReferenceableOptional)
            {
                var id = (value as IReferenceableOptional).id;
                writer.WriteValue(id);
            }
            if (value.GetType().IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                writer.WriteStartObject();
                foreach (var kvpObj in value.DictionaryKeyValuePairs())
                {
                    var keyValue = kvpObj.Key;
                    var propertyName = (keyValue is IReferenceable)?
                        (keyValue as IReferenceable).id.ToString("N")
                        :
                        keyValue.ToString();
                    writer.WritePropertyName(propertyName);

                    var valueValue = kvpObj.Value;
                    writer.WriteValue(valueValue);
                }
                writer.WriteEndObject();
            }
            if (value is Type)
            {
                var stringType = (value as Type).GetClrString();
                writer.WriteValue(stringType);
            }
        }
    }

}
