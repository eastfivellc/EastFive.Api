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

namespace EastFive.Api.Serialization
{
    public class ExtrudeConvert : Newtonsoft.Json.JsonConverter
    {
        HttpApplication application;
        UrlHelper urlHelper;
        bool useWebIds;

        public ExtrudeConvert(HttpApplication httpApplication, HttpRequestMessage request)
        {
            var urlHelper = request.GetUrlHelper();
            var useWebIds = request.Headers.Accept.Contains(
                header =>
                {
                    if (header.MediaType.ToLower() != "application/json")
                        return false;
                    var requestWebId = header.Parameters.Contains(
                        nhv =>
                        {
                            if (nhv.Name.ToLower() != "id")
                                return false;
                            if (nhv.Value.ToLower() == "webid")
                                return true;
                            return false;
                        });
                    return requestWebId;
                });

            this.application = httpApplication;
            this.urlHelper = urlHelper;
            this.useWebIds = useWebIds;
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
                if (objectType.GetGenericArguments().Any(arg => CanConvert(arg)))
                    return true;
            }
            if (objectType.IsSubclassOf(typeof(Type)))
                return true;
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Extruder does not read values");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            void WriteId(Guid? idMaybe)
            {
                if (!idMaybe.HasValue)
                {
                    writer.WriteValue((string)null);
                    return;
                }

                var id = idMaybe.Value;
                if (!useWebIds)
                {
                    writer.WriteValue(id);
                    return;
                }

                // The webID could be created and then serialized, etc. 
                // or.... it can just be writen inline here.

                writer.WriteStartObject();
                var key = id.ToString();
                writer.WritePropertyName("key");
                writer.WriteValue(key);

                var uuid = id;
                writer.WritePropertyName("uuid");
                writer.WriteValue(uuid);

                var valueIdType = value.GetType();
                if (!valueIdType.IsGenericType)
                {
                    writer.WriteEndObject();
                    return;
                }

                // TODO: Handle dictionary, etc
                var refType = valueIdType.GetGenericArguments().First();
                var webId = urlHelper.GetWebId(refType, id);

                var contentType = $"x-application/x-{refType.Name.ToLower()}";
                if (refType.ContainsCustomAttribute<FunctionViewControllerAttribute>(true))
                {
                    var fvcAttrContentType = refType.GetCustomAttribute<FunctionViewControllerAttribute>().ContentType;
                    if (fvcAttrContentType.HasBlackSpace())
                        contentType = fvcAttrContentType;
                }
                var applicationNamespace = application.Namespace;
                var urnString = $"urn:{contentType}:{applicationNamespace}:{key}";
                writer.WritePropertyName("urn");
                writer.WriteValue(urnString);

                var source = urlHelper.GetLocationWithId(refType, id);
                writer.WritePropertyName("source");
                writer.WriteValue(source.AbsoluteUri);
                writer.WriteEndObject();

                return;

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
            if(value == null)
            {
                writer.WriteNull();
                return;
            }
            var valueType = value.GetType();
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
                    if (this.CanConvert(valueType.GenericTypeArguments.Last()))
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
                var serializationAttrs = typeValue.GetAttributesInterface<IProvideSerialization>();
                if(serializationAttrs.Any())
                {
                    var serializationAttr = serializationAttrs.First();
                    writer.WriteValue(serializationAttr.ContentType);
                    return;
                }
                var stringType = typeValue.GetClrString();
                writer.WriteValue(stringType);
                return;
            }
        }
    }
}
