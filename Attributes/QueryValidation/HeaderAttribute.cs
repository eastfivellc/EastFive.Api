using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using EastFive.Api.Core;
using EastFive.Api.Serialization;
using EastFive.Linq;

namespace EastFive.Api
{
    public class HeaderAttribute : QueryValidationAttribute, IBindJsonApiValue
    {
        public string Content { get; set; }

        public TResult ParseContentDelegate<TResult>(JObject contentJObject, string contentString, 
                BindConvert bindConvert, 
                ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request, 
            Func<object, TResult> onParsed, 
            Func<string, TResult> onFailure)
        {
            var key = Content;
            if (!contentJObject.TryGetValue(key, out JToken valueToken))
                return onFailure($"Key[{key}] was not found in JSON");

            if (typeof(System.Net.Http.Headers.MediaTypeHeaderValue) == parameterInfo.ParameterType)
            {
                var contentEncodedBase64String = valueToken.Value<string>();
                var mediaHeaderType = contentEncodedBase64String.MatchRegexInvoke(
                    "data:(?<contentType>[^;]+);base64,.+",
                    (contentType) => new MediaTypeHeaderValue(contentType),
                    types => types.First(
                        (ct, next) => ct,
                        () => new MediaTypeHeaderValue("application/data")));
                return onParsed(mediaHeaderType);
            }

            throw new NotImplementedException();
        }

        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var request = bindingData.request;
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var bindType = parameterRequiringValidation.ParameterType;
            request.GetAbsoluteUri();
            if (typeof(MediaTypeHeaderValue) == bindType)
            {
                if (Content.IsNullOrWhiteSpace())
                    return new SelectParameterResult
                    {
                        valid = true,
                        fromBody = false,
                        fromQuery = false,
                        fromFile = false,
                        key = Content,
                        parameterInfo = parameterRequiringValidation,
                        value = request.GetMediaType(),
                    };

                return bindingData.fetchBodyParam(
                    parameterRequiringValidation,
                    (value) =>
                    {
                        return new SelectParameterResult
                        {
                            valid = true,
                            fromBody = false,
                            fromQuery = false,
                            fromFile = false,
                            key = Content,
                            parameterInfo = parameterRequiringValidation,
                            value = value,
                        };
                    },
                    (why) =>
                    {
                        return new SelectParameterResult
                        {
                            fromBody = false,
                            key = "",
                            fromQuery = false,
                            fromFile = false,
                            parameterInfo = parameterRequiringValidation,
                            valid = false,
                            failure = why, // $"Cannot extract MediaTypeHeaderValue from non-multipart request.",
                        };
                    });
            }
            if (bindType.IsSubClassOfGeneric(typeof(HttpHeaderValueCollection<>)))
            {
                if (bindType.GenericTypeArguments.First() == typeof(StringWithQualityHeaderValue))
                {
                    if (Content.HasBlackSpace())
                        return new SelectParameterResult
                        {
                            key = "",
                            fromQuery = false,
                            fromBody = false,
                            fromFile = false,
                            parameterInfo = parameterRequiringValidation,
                            valid = false,
                            failure = "AcceptLanguage is not a content header.",
                        };
                    return new SelectParameterResult
                    {
                        valid = true,
                        fromBody = false,
                        fromQuery = false,
                        fromFile = false,
                        key = default,
                        parameterInfo = parameterRequiringValidation,
                        value = request.GetAcceptLanguage(),
                    };
                }
            }
            return new SelectParameterResult
            {
                fromBody = false,
                key = "",
                fromQuery = false,
                parameterInfo = parameterRequiringValidation,
                valid = false,
                failure = $"No header binding for type `{bindType.FullName}`.",
            };
        }
    }

    public class HeaderOptionalAttribute : HeaderAttribute
    {
        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var baseValue = base.TryCast(bindingData);
            if (baseValue.valid)
                return baseValue;

            baseValue.valid = true;
            baseValue.fromBody = true;
            baseValue.value = parameterRequiringValidation.ParameterType.GetDefault();
            return baseValue;
        }
    }
}
