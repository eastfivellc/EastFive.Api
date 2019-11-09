using EastFive.Api.Bindings;
using EastFive.Api.Resources;
using EastFive.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EastFive.Api
{
    public class PropertyAttribute : QueryValidationAttribute,
        IDocumentParameter, IBindJsonApiValue
    {
        public override SelectParameterResult TryCast(IApplication httpApp,
                HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
                CastDelegate fetchQueryParam,
                CastDelegate fetchBodyParam,
                CastDelegate fetchDefaultParam)
        {
            var name = this.GetKey(parameterRequiringValidation);
            return fetchBodyParam(parameterRequiringValidation,
                vCasted => SelectParameterResult.Body(vCasted, name, parameterRequiringValidation),
                why => SelectParameterResult.FailureBody(why, name, parameterRequiringValidation));
        }

        public virtual TResult Convert<TResult>(HttpApplication httpApp, Type type, object value,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            if (value.IsDefaultOrNull())
            {
                return onCasted(type.GetDefault());
            }

            if (type.IsAssignableFrom(value.GetType()))
                return onCasted(value);

            if (value is BlackBarLabs.Api.Resources.WebId)
            {
                var webId = value as BlackBarLabs.Api.Resources.WebId;
                if (typeof(Guid).GUID == type.GUID)
                {
                    if (webId.IsDefaultOrNull())
                        return onInvalid("Value did not provide a UUID.");
                    return onCasted(webId.UUID);
                }
                if (typeof(Guid?).GUID == type.GUID)
                {
                    if (webId.IsDefaultOrNull())
                        return onCasted(default(Guid?));
                    var valueGuidMaybe = (Guid?)webId.UUID;
                    return onCasted(valueGuidMaybe);
                }
            }

            if (value is Guid?)
            {
                var guidMaybe = value as Guid?;
                if (typeof(Guid).GUID == type.GUID)
                {
                    if (!guidMaybe.HasValue)
                        return onInvalid("Value did not provide a UUID.");
                    return onCasted(guidMaybe.Value);
                }
                if (typeof(Guid?).GUID == type.GUID)
                {
                    return onCasted(guidMaybe);
                }
            }

            if (value is string)
            {
                var valueString = value as string;
                if (typeof(Guid).GUID == type.GUID)
                {
                    if (Guid.TryParse(valueString, out Guid valueGuid))
                        return onCasted(valueGuid);
                    return onInvalid($"[{valueString}] is not a valid UUID.");
                }
                if (typeof(Guid?).GUID == type.GUID)
                {
                    if (valueString.IsNullOrWhiteSpace())
                        return onCasted(default(Guid?));
                    if (Guid.TryParse(valueString, out Guid valueGuid))
                        return onCasted(valueGuid);
                    return onInvalid($"[{valueString}] needs to be empty or a valid UUID.");
                }
                if (typeof(DateTime).GUID == type.GUID)
                {
                    if (DateTime.TryParse(valueString, out DateTime valueDateTime))
                        return onCasted(valueDateTime);
                    return onInvalid($"[{valueString}] needs to be a valid date/time.");
                }
                if (typeof(DateTime?).GUID == type.GUID)
                {
                    if (valueString.IsNullOrWhiteSpace())
                        return onCasted(default(DateTime?));
                    if (DateTime.TryParse(valueString, out DateTime valueDateTime))
                        return onCasted(valueDateTime);
                    return onInvalid($"[{valueString}] needs to be empty or a valid date/time.");
                }

                if (type.IsEnum)
                {
                    if (Enum.IsDefined(type, valueString))
                    {
                        var valueEnum = Enum.Parse(type, valueString);
                        return onCasted(valueEnum);
                    }
                    return onInvalid($"{valueString} is not one of [{Enum.GetNames(type).Join(",")}]");
                }

                if (typeof(Type).GUID == type.GUID)
                {
                    return HttpApplication.GetResourceType(valueString,
                            (typeInstance) => onCasted(typeInstance),
                            () => valueString.GetClrType(
                                typeInstance => onCasted(typeInstance),
                                () => onInvalid(
                                    $"`{valueString}` is not a recognizable resource type or CLR type.")));
                }
            }

            if (value is int)
            {
                if (type.IsEnum)
                {
                    var valueInt = (int)value;
                    var valueEnum = Enum.ToObject(type, valueInt);
                    return onCasted(valueEnum);
                }
            }

            if (value.GetType().IsArray)
            {
                if (type.IsArray)
                {
                    var elementType = type.GetElementType();
                    var array = (object[])value;

                    //var casted = Array.ConvertAll(array,
                    //    item => item.ToString());
                    //var typeConverted = casted.Cast<int>().ToArray();

                    var casted = Array.ConvertAll(array,
                        item => Convert(httpApp, elementType, item, (v) => v, (why) => elementType.GetDefault()));
                    var typeConvertedEnumerable = typeof(System.Linq.Enumerable)
                        .GetMethod("Cast", BindingFlags.Static | BindingFlags.Public)
                        .MakeGenericMethod(new Type[] { elementType })
                        .Invoke(null, new object[] { casted });
                    var typeConvertedArray = typeof(System.Linq.Enumerable)
                        .GetMethod("ToArray", BindingFlags.Static | BindingFlags.Public)
                        .MakeGenericMethod(new Type[] { elementType })
                        .Invoke(null, new object[] { typeConvertedEnumerable });

                    return onCasted(typeConvertedArray);
                }
            }

            return onInvalid($"Could not convert `{value.GetType().FullName}` to `{type.FullName}`.");
        }

        public virtual Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Parameter()
            {
                Default = false,
                Name = this.GetKey(paramInfo),
                Required = true,
                Type = Parameter.GetTypeName(paramInfo.ParameterType, httpApp),
                Where = "BODY",
            };
        }

        public TResult ParseContentDelegate<TResult>(JObject contentJObject,
                string contentString, Serialization.BindConvert bindConvert,
                ParameterInfo paramInfo,
                IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var key = this.GetKey(paramInfo);
            return ParseJsonContentDelegate(contentJObject,
                    contentString, bindConvert,
                    key, paramInfo,
                    httpApp, request,
                onParsed,
                onFailure);
        }

        public static TResult ParseJsonContentDelegate<TResult>(JObject contentJObject,
                string contentString, Serialization.BindConvert bindConvert,
                string key, ParameterInfo paramInfo,
                IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (key.IsNullOrWhiteSpace() || key == ".")
            {
                try
                {
                    var rootObject = Newtonsoft.Json.JsonConvert.DeserializeObject(
                        contentString, paramInfo.ParameterType, bindConvert);
                    return onParsed(rootObject);
                }
                catch (Exception ex)
                {
                    return onFailure(ex.Message);
                }
            }

            if (!contentJObject.TryGetValue(key, out JToken valueToken))
                return onFailure($"Key[{key}] was not found in JSON");

            try
            {
                var tokenParser = new Serialization.JsonTokenParser(valueToken);
                return paramInfo.Bind(valueToken, httpApp,
                    obj => onParsed(obj),
                    (why) =>
                    {
                        if (valueToken.Type == JTokenType.Object || valueToken.Type == JTokenType.Array)
                        {
                            try
                            {
                                var value = Newtonsoft.Json.JsonConvert.DeserializeObject(
                                    valueToken.ToString(), paramInfo.ParameterType, bindConvert);
                                return onParsed(value);
                            }
                            catch (Newtonsoft.Json.JsonSerializationException)
                            {
                                throw;
                            }
                        }
                        return onFailure(why);
                    });
            }
            catch (Exception ex)
            {
                return onFailure(ex.Message);
            }
        }
    }
}
