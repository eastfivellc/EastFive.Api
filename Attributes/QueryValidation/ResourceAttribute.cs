using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Extensions;
using System.Xml;
using EastFive.Api.Serialization;
using Newtonsoft.Json.Linq;
using EastFive.Linq.Expressions;
using System.IO;
using EastFive.Api.Bindings;

namespace EastFive.Api
{
    public class ResourceAttribute : System.Attribute, IBindApiValue, IBindJsonApiValue, IBindMultipartApiValue
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

        public TResult ParseContentDelegate<TResult>(JObject contentJObject,
                string contentString, BindConvert bindConvert, ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            try
            {
                var issue = parameterInfo.ParameterType.GetMembers().ToArray();
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
            var obj = paramType.GetMembers()
                .Aggregate(Activator.CreateInstance(paramType),
                    (param, member) =>
                    {
                        if (!member.ContainsAttributeInterface<IProvideApiValue>(true))
                            return param;
                        var provideApiValue = member.GetAttributeInterface<IProvideApiValue>(true);

                        if (!content.ContainsKey(provideApiValue.PropertyName))
                            return param;
                        
                        var tokenParser = content[provideApiValue.PropertyName];
                        return ContentToType(httpApp, member.GetMemberType(),
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

        internal static TResult ContentToType<TResult>(IApplication httpApp, Type type,
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
            return httpApp.Bind(strValue, type,
                (value) =>
                {
                    return onParsed(value);
                },
                why => onFailure(why));
        }
    }
}
