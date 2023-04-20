using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Api.Serialization;
using Newtonsoft.Json.Linq;
using EastFive.Reflection;
using EastFive.Api.Bindings;
using Microsoft.AspNetCore.Http;

namespace EastFive.Api
{
    public class ResourceAttribute : System.Attribute, 
        IBindApiValue, IBindJsonApiValue, IBindMultipartApiValue, IBindFormDataApiValue
    {
        public string GetKey(ParameterInfo paramInfo)
        {
            return default;
        }

        public SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            return bindingData.fetchBodyParam(parameterRequiringValidation,
                (value) => SelectParameterResult.Body(value, string.Empty, parameterRequiringValidation),
                (why) => SelectParameterResult.FailureBody(why, string.Empty, parameterRequiringValidation));
        }

        public TResult ParseContentDelegate<TResult>(JContainer contentJObject,
                string contentString, BindConvert bindConvert, ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            try
            {
                var rootObject = Newtonsoft.Json.JsonConvert.DeserializeObject(
                    contentString, parameterInfo.ParameterType, bindConvert);
                return onParsed(rootObject);
            }
            catch (Exception ex)
            {
                return onFailure(ex.Message);
            }
        }

        public TResult ParseContentDelegate<TResult>(
                IDictionary<string, MultipartContentTokenParser> content, 
                ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request, 
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var paramType = parameterInfo.ParameterType;
            var obj = paramType
                .GetPropertyAndFieldsWithAttributesInterface<IProvideApiValue>(true)
                .Aggregate(Activator.CreateInstance(paramType),
                    (param, memberProvideApiValueTpl) =>
                    {
                        var (member, provideApiValue) = memberProvideApiValueTpl;

                        var propertyName = provideApiValue.GetPropertyName(member);
                        if (!content.ContainsKey(propertyName))
                            return param;
                        
                        var tokenParser = content[propertyName];
                        return ContentToType(httpApp, member.GetMemberType(), parameterInfo,
                            tokenParser,
                            paramValue =>
                            {
                                member.SetValue(ref param, paramValue);
                                return param;
                            },
                            why => param);
                    });
            return onParsed(obj);
        }

        internal static TResult ContentToType<TResult>(IApplication httpApp,
                Type type, ParameterInfo parameterInfo,
                MultipartContentTokenParser tokenReader,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (type.IsAssignableFrom(typeof(Stream)))
            {
                var streamValue = tokenReader.ReadStream();
                return onParsed((object)streamValue);
            }
            if (type.IsAssignableFrom(typeof(byte[])))
            {
                var byteArrayValue = tokenReader.ReadBytes();
                return onParsed((object)byteArrayValue);
            }
            if (type.IsAssignableFrom(typeof(HttpContent)))
            {
                var content = tokenReader.ReadObject<HttpContent>();
                return onParsed((object)content);
            }
            if (type.IsAssignableFrom(typeof(System.Net.Http.Headers.ContentDispositionHeaderValue)))
            {
                var content = tokenReader.ReadObject<HttpContent>();
                var header = content.Headers.ContentDisposition;
                return onParsed((object)header);
            }
            if (type.IsAssignableFrom(typeof(ByteArrayContent)))
            {
                var content = tokenReader.ReadObject<ByteArrayContent>();
                return onParsed((object)content);
            }
            var strValue = tokenReader.ReadString();
            return httpApp.Bind(strValue, parameterInfo,
                (value) =>
                {
                    return onParsed(value);
                },
                why => onFailure(why));
        }

        public TResult ParseContentDelegate<TResult>(IFormCollection formData,
                ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var paramType = parameterInfo.ParameterType;
            var obj = paramType
                .GetPropertyAndFieldsWithAttributesInterface<IProvideApiValue>(true)
                .Aggregate(Activator.CreateInstance(paramType),
                    (param, memberProvideApiValueTpl) =>
                    {
                        var (member, provideApiValue) = memberProvideApiValueTpl;

                        return ParseFormContentDelegate(provideApiValue.GetPropertyName(member), formData,
                                member.GetMemberType(), parameterInfo, httpApp,
                            paramValue =>
                            {
                                member.SetValue(ref param, paramValue);
                                return param;
                            },
                            why =>
                            {
                                return httpApp.Bind<string, object>(default(string), member,
                                    defaultValue =>
                                    {
                                        member.SetValue(ref param, defaultValue);
                                        return param;
                                    },
                                    why => param);
                            });
                    });
            return onParsed(obj);
        }

        public static TResult ParseFormContentDelegate<TResult>(string key, IFormCollection formData,
                MemberInfo member, ParameterInfo parameter, IApplication httpApp,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (formData.IsDefaultOrNull())
                return onFailure("No form data provided");

            return formData
                .Where(kvp => kvp.Key == key)
                .First(
                    (kvp, next) =>
                    {
                        var strValue = (string)kvp.Value;
                        return httpApp.Bind(strValue, parameter,
                            (value) =>
                            {
                                return onParsed(value);
                            },
                            why =>
                            {
                                return httpApp.Bind(strValue, member,
                                    (value) =>
                                    {
                                        return onParsed(value);
                                    },
                                    why => onFailure(why));
                            });
                    },
                    () =>
                    {
                        return formData.Files
                            .Where(file => file.Name == key)
                            .First(
                                (fileValue, next) =>
                                {
                                    return httpApp.Bind(fileValue, member,
                                        (value) =>
                                        {
                                            return onParsed(value);
                                        },
                                        why =>
                                        {
                                            return httpApp.Bind(fileValue, member.GetType(),
                                                (value) =>
                                                {
                                                    return onParsed(value);
                                                },
                                                why => onFailure(why));
                                        });
                                },
                                () => onFailure("Key not found"));
                    });

        }
    }
}
