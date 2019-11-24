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

            if (type.IsSubClassOfGeneric(typeof(IRef<>)))
            {
                var activatableType = typeof(Ref<>).MakeGenericType(type.GenericTypeArguments);
                if (content.Type == JTokenType.Guid)
                {
                    var guidValue = content.Value<Guid>();
                    var iref = Activator.CreateInstance(activatableType, guidValue);
                    return onParsed(iref);
                }
                if (content.Type == JTokenType.String)
                {
                    return StandardStringBindingsAttribute.BindDirect(type,
                            content.Value<string>(),
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                if (content.Type == JTokenType.Object)
                {
                    var objectContent = (content as JObject);
                    if (objectContent.TryGetValue("uuid", StringComparison.OrdinalIgnoreCase, out JToken idContent))
                        return BindDirect(type, idContent, onParsed, onDidNotBind, onBindingFailure);
                    var guidStr = objectContent.ToString();
                    return StandardStringBindingsAttribute.BindDirect(type,
                            guidStr,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
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

            if (type == typeof(int))
            {
                if (content.Type == JTokenType.Float)
                {
                    var floatValue = content.Value<double>();
                    var intValue = (int)floatValue;
                    return onParsed(intValue);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var intValue = content.Value<int>();
                    return onParsed(intValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(int).FullName}");
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

            if (type == typeof(DateTime))
            {
                if (content.Type == JTokenType.Date)
                {
                    var dateValue = content.Value<DateTime>();
                    return onParsed(dateValue);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var intValue = content.Value<long>();
                    var dateValue = new DateTime(intValue);
                    return onParsed(dateValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(DateTime).FullName}");
            }

            if (type == typeof(bool))
            {
                if (content.Type == JTokenType.Boolean)
                {
                    var boolValue = content.Value<bool>();
                    return onParsed(boolValue);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var intValue = content.Value<int>();
                    var boolValue = intValue != 0;
                    return onParsed(boolValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(bool).FullName}");
            }

            if (type.IsNullable())
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
                return GetGuid(
                    (id) =>
                    {
                        var refType = typeof(Ref<>).MakeGenericType(objectType.GenericTypeArguments);
                        var value = Activator.CreateInstance(refType, id);
                        return onParsed(value);
                    },
                    onDidNotBind,
                    onBindingFailure);
            }

            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
            {
                return GetGuidMaybe(
                    id =>
                    {
                        var refType = typeof(RefOptional<>).MakeGenericType(objectType.GenericTypeArguments);
                        var value = Activator.CreateInstance(refType, id);
                        return onParsed(value);
                    });
            }

            if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
            {
                return GetGuids(
                    ids =>
                    {
                        var refType = typeof(Refs<>).MakeGenericType(objectType.GenericTypeArguments);
                        var value = Activator.CreateInstance(refType, ids);
                        return onParsed(value);
                    });
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

            if (objectType.IsNullable())
            {
                var underlyingType = objectType.GetNullableUnderlyingType();
                if (reader.TokenType == JsonToken.Null)
                    return onParsed(objectType.GetDefault());
                return Bind(underlyingType, reader,
                    obj => onParsed(obj.AsNullable()),
                    (why) => onParsed(objectType.GetDefault()),
                    (why) => onParsed(objectType.GetDefault()));
            }

            // As a last ditch effort, see if the JToken deserialization will work.
            var token = JToken.ReadFrom(reader);
            return BindDirect(objectType, token,
                onParsed,
                onDidNotBind,
                onBindingFailure);


            TR GetGuid<TR>(
                Func<Guid, TR> onGot,
                Func<string, TR> onIgnored,
                Func<string, TR> onFailed)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var guidString = reader.Value as string;
                    var guid = Guid.Parse(guidString);
                    return onGot(guid);
                }
                if (reader.TokenType == JsonToken.StartObject)
                {
                    if (!reader.Read())
                        return onIgnored("Empty object");
                    return GetGuid(
                        guid =>
                        {
                            while (reader.TokenType != JsonToken.EndObject)
                            {
                                if (!reader.Read())
                                    break;
                            }
                            return onGot(guid);
                        },
                        onIgnored,
                        onFailed);
                }
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    var propertyName = reader.Value as string;
                    if (!reader.Read())
                        return onFailed("Property did not have value.");
                    if (propertyName.ToLower() == "uuid")
                        return GetGuid(onGot, onIgnored, onFailed);
                    if (!reader.Read())
                        return onIgnored("'uuid' Property not found.");
                    return GetGuid(onGot, onIgnored, onFailed);
                }
                return onFailed($"Cannot decode token of type `{reader.TokenType}` to UUID.");
            }

            TResult GetGuidMaybe(Func<Guid?, TResult> callback)
            {
                if (reader.TokenType == JsonToken.Null)
                    return callback(default(Guid?));
                return GetGuid(
                    (x) => callback(x),
                    onDidNotBind,
                    onBindingFailure);
            }

            TResult GetGuids(Func<Guid[], TResult> onGot)
            {
                if (reader.TokenType == JsonToken.Null)
                    return onGot(new Guid[] { });

                if (reader.TokenType == JsonToken.StartArray)
                {
                    var list = new List<Guid>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.EndArray)
                            break;

                        var result = GetGuid(
                            g => new
                            {
                                why = string.Empty,
                                g = g.AsOptional(),
                                success = true,
                            },
                            why => new
                            {
                                why = why,
                                g = default(Guid?),
                                success = true,
                            },
                            why => new
                            {
                                why = why,
                                g = default(Guid?),
                                success = false,
                            });

                        if (!result.success)
                            return onBindingFailure(result.why);
                        if (result.g.HasValue)
                            list.Add(result.g.Value);
                    }
                    return onGot(list.ToArray());
                }

                return onBindingFailure($"Cannot decode token of type `{reader.TokenType}` to {objectType.FullName}.");
            }
        }
    }
}
