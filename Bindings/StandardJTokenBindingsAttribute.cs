using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Reflection;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using EastFive.Linq;

namespace EastFive.Api.Bindings
{
    public class StandardJTokenBindingsAttribute : Attribute,
        IBindApiParameter<JToken>,
        IBindApiParameter<JsonReader>
    {
        public TResult Bind<TResult>(Type type, JToken content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            return BindDirect(type, content,
                onParsed,
                onDidNotBind, 
                onBindingFailure);
        }

        public static TResult BindDirect<TResult>(Type type, JToken content, 
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            if(type.IsAssignableFrom(typeof(Guid)))
            {
                if (content.Type == JTokenType.Guid)
                {
                    var guidValue = content.Value<Guid>();
                    return onParsed(guidValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    if(Guid.TryParse(stringValue, out Guid guidValue))
                        return onParsed(guidValue);
                    return onBindingFailure($"Cannot convert `{stringValue}` to Guid.");
                }
                var webId = ReadObject<WebId>(content);
                if (webId.IsDefaultOrNull())
                    return onBindingFailure("Null value for GUID.");
                var guidValueMaybe = webId.ToGuid();
                if (!guidValueMaybe.HasValue)
                    return onBindingFailure("Null WebId cannot be converted to a Guid.");
                var webIdGuidValue = guidValueMaybe.Value;
                return onParsed(webIdGuidValue);
            }

            if (type.IsSubClassOfGeneric(typeof(IRefs<>)))
            {
                var activatableType = typeof(Refs<>).MakeGenericType(type.GenericTypeArguments);
                if (content.Type == JTokenType.Guid)
                {
                    var guidValue = content.Value<Guid>();
                    var guids = guidValue.AsArray();
                    var irefs = Activator.CreateInstance(activatableType, guids);
                    return onParsed(irefs);
                }
                if (content.Type == JTokenType.String)
                {
                    return StandardStringBindingsAttribute.BindDirect(type,
                            content.Value<string>(),
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                if (content.Type == JTokenType.Array)
                {
                    var guids = ReadArray(content)
                        .Select(
                            token => BindDirect(typeof(Guid), token,
                                guid => (Guid?)((Guid)guid),
                                (why) => default(Guid?),
                                (why) => default(Guid?)))
                        .SelectWhereHasValue()
                        .ToArray();
                    
                    var irefs = Activator.CreateInstance(activatableType, guids);
                    return onParsed(irefs);
                }
            }

            if (type == typeof(double))
            {
                if(content.Type == JTokenType.Float)
                {
                    var floatValue = content.Value<double>();
                    return onParsed(floatValue);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var intValue = content.Value<int>();
                    var floatValue = (double)intValue;
                    return onParsed(floatValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue,
                        onParsed, 
                        onDidNotBind,
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(double).FullName}");
            }

            if (type == typeof(decimal))
            {
                if (content.Type == JTokenType.Float)
                {
                    var floatValue = content.Value<double>();
                    var decimalValule = (decimal)floatValue;
                    return onParsed(decimalValule);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var intValue = content.Value<int>();
                    var floatValue = (decimal)intValue;
                    return onParsed(floatValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue, 
                        onParsed,
                        onDidNotBind, 
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(decimal).FullName}");
            }

            if(type.IsNullable())
            {
                var nullableT = type.GetNullableUnderlyingType();
                return BindDirect(nullableT, content,
                    (v) =>
                    {
                        var nullableV = v.AsNullable();
                        return onParsed(nullableV);
                    },
                    (why) => onParsed(type.GetDefault()),
                    (why) => onParsed(type.GetDefault()));
            }

            if (type.IsAssignableFrom(typeof(IDictionary<,>)))
            {
                var keyType = type.GenericTypeArguments[0];
                var valueType = type.GenericTypeArguments[1];
                var refType = typeof(Dictionary<,>).MakeGenericType(type.GenericTypeArguments);
                var refInstance = Activator.CreateInstance(refType);
                var addMethod = refType.GetMethod("Add");
                //Dictionary<string, int> dict;
                //dict.Add()
                foreach (var kvpToken in ReadDictionary(content))
                {
                    var keyToken = kvpToken.Key;
                    var valueToken = kvpToken.Value;
                    string result = StandardStringBindingsAttribute.BindDirect(keyType, keyToken,
                        keyValue =>
                        {
                            return BindDirect(valueType, valueToken,
                                valueValue =>
                                {
                                    addMethod.Invoke(refInstance,
                                        new object[] { keyValue, valueValue });
                                    return string.Empty;
                                },
                                (why) => why,
                                (why) => why);
                        },
                        (why) => why,
                        (why) => why);
                }

                return onParsed(refInstance);
            }

            if (type == typeof(object))
            {
                var objectValue = ReadObject(content);
                return onParsed(objectValue);
            }

            if (content is JObject)
            {
                var jObj = content as JObject;
                var jsonText = jObj.ToString();
                var value = JsonConvert.DeserializeObject(jsonText, type);
                return onParsed(value);
            }

            if (content.Type == JTokenType.String)
            {
                return StandardStringBindingsAttribute.BindDirect(type,
                        content.Value<string>(),
                    onParsed,
                    onDidNotBind,
                    onBindingFailure);
            }

            if (content.Type == JTokenType.Null)
            {
                var defaultValue = type.GetDefault();
                return onParsed(defaultValue);
            }

            return onDidNotBind($"Could not find binding for type {type.FullName}");
        }


        public  static T ReadObject<T>(JToken valueToken)
        {
            if (valueToken is JObject)
            {
                var jObj = valueToken as Newtonsoft.Json.Linq.JObject;
                var jsonText = jObj.ToString();
                var value = JsonConvert.DeserializeObject<T>(jsonText);
                return value;
            }
            return valueToken.Value<T>();
        }

        public static object ReadObject(JToken valueToken)
        {
            var token = valueToken;
            if (token is JValue)
            {
                if (token.Type == JTokenType.Boolean)
                    return token.Value<bool>();
                if (token.Type == JTokenType.Bytes)
                    return token.Value<byte[]>();
                if (token.Type == JTokenType.Date)
                    return token.Value<DateTime>();
                if (token.Type == JTokenType.Float)
                    return token.Value<float>();
                if (token.Type == JTokenType.Guid)
                    return token.Value<Guid>();
                if (token.Type == JTokenType.Integer)
                    return token.Value<int>();
                if (token.Type == JTokenType.None)
                    return null;
                if (token.Type == JTokenType.Null)
                    return null;
                if (token.Type == JTokenType.String)
                    return token.Value<string>();
                if (token.Type == JTokenType.Uri)
                    return token.Value<Uri>();
            }
            return token.ToObject<object>();
        }

        public static JToken[] ReadArray(JToken valueToken)
        {
            if (valueToken.Type == JTokenType.Array)
                return (valueToken as JArray)
                    .Select(
                        token => token)
                    .ToArray();
            if (valueToken.Type == JTokenType.Null)
                return new JToken[] { };
            if (valueToken.Type == JTokenType.Undefined)
                return new JToken[] { };

            if (valueToken.Type == JTokenType.Object)
                return valueToken
                    .Children()
                    .Select(childToken => childToken)
                    .ToArray();

            if (valueToken.Type == JTokenType.Property)
            {
                var property = (valueToken as JProperty);
                return property.Value.AsArray();
            }

            return valueToken
                .Children()
                .ToArray();
        }

        public static IDictionary<string, JToken> ReadDictionary(JToken valueToken)
        {
            KeyValuePair<string, JToken> ParseToken(JToken token)
            {
                if (token.Type == JTokenType.Property)
                {
                    var propertyToken = (token as JProperty);
                    return propertyToken.Value
                        .PairWithKey<string, JToken>(propertyToken.Name);
                }
                return token
                    .PairWithKey<string, JToken>(token.ToString());
            }

            if (valueToken.Type == JTokenType.Array)
                return (valueToken as JArray)
                    .Select(ParseToken)
                    .ToDictionary();
            if (valueToken.Type == JTokenType.Null)
                return new Dictionary<string, JToken>();
            if (valueToken.Type == JTokenType.Undefined)
                return new Dictionary<string, JToken>();

            if (valueToken.Type == JTokenType.Object)
                return valueToken
                    .Children()
                    .Select(ParseToken)
                    .ToDictionary();

            return valueToken
                    .Children()
                    .Select(ParseToken)
                    .ToDictionary();
        }

        public TResult Bind<TResult>(Type objectType, JsonReader reader, 
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
            {
                var id = GetGuid();
                var refType = typeof(Ref<>).MakeGenericType(objectType.GenericTypeArguments);
                var value = Activator.CreateInstance(refType, id);
                return onParsed(value);
            }

            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
            {
                var id = GetGuidMaybe();
                var refType = typeof(RefOptional<>).MakeGenericType(objectType.GenericTypeArguments);
                var value = Activator.CreateInstance(refType, id);
                return onParsed(value);
            }

            if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
            {
                var ids = GetGuids();
                var refType = typeof(Refs<>).MakeGenericType(objectType.GenericTypeArguments);
                var value = Activator.CreateInstance(refType, ids);
                return onParsed(value);
            }

            if (objectType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                var dictionaryKeyType = objectType.GenericTypeArguments[0];
                var dictionaryValueType = objectType.GenericTypeArguments[1];
                var dictionaryType = typeof(Dictionary<,>).MakeGenericType(objectType.GenericTypeArguments);
                var instance = Activator.CreateInstance(dictionaryType);

                if (reader.TokenType != JsonToken.StartObject)
                    return onParsed(instance);
                if (!reader.Read())
                    return onParsed(instance);

                var addMethod = dictionaryType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
                do
                {
                    if (reader.TokenType == JsonToken.EndObject)
                        return onParsed(instance);
                    instance = StandardStringBindingsAttribute.BindDirect(dictionaryKeyType, reader.Path,
                        keyValue =>
                        {
                            var valueValue = Bind(dictionaryValueType, reader,
                                v => v,
                                why => dictionaryValueType.GetDefault(),
                                why => dictionaryValueType.GetDefault());
                            addMethod.Invoke(instance, new object[] { keyValue, valueValue });
                            return instance;
                        },
                        (why) => instance,
                        (why) => instance);
                    
                } while (reader.Read());
            }

            if (objectType == typeof(byte[]))
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var bytesString = reader.Value as string;
                    var value = bytesString.FromBase64String();
                    return onParsed(value);
                }
            }
            
            if(objectType.IsNullable())
            {
                return Bind(objectType.GetNullableUnderlyingType(), reader,
                    obj => onParsed(obj.AsNullable()),
                    (why) => onParsed(objectType.GetDefault()),
                    (why) => onParsed(objectType.GetDefault()));
            }

            // As a last ditch effort, see if the JToken deserialization will work.
            var token = JToken.ReadFrom(reader);
            return Bind(objectType, token,
                onParsed,
                onDidNotBind,
                onBindingFailure);


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
        }
    }
}
