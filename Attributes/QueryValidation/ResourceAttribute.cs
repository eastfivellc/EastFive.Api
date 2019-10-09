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

namespace EastFive.Api
{
    public class ResourceAttribute : System.Attribute, IBindApiValue, IBindJsonApiValue
    {
        public string GetKey(ParameterInfo paramInfo)
        {
            return default;
        }

        public Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
            HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate<SelectParameterResult> fetchQueryParam,
            CastDelegate<SelectParameterResult> fetchBodyParam,
            CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
        {
            return fetchBodyParam(parameterRequiringValidation,
                (value) => SelectParameterResult.Body(value, string.Empty, parameterRequiringValidation),
                (why) => SelectParameterResult.FailureBody(why, string.Empty, parameterRequiringValidation));
        }

        public RequestMessage<TResource> BindContent<TResource>(RequestMessage<TResource> request,
            MethodInfo method, ParameterInfo parameter, object contentObject)
        {
            var contentJsonString = JsonConvert.SerializeObject(contentObject, new Serialization.Converter());
            var stream = contentJsonString.ToStream();
            var content = new StreamContent(stream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return request.SetContent(content);
        }

        public async Task<TResult> ParseContentDelegateAsync<TResult>(JObject contentJObject,
                string contentString, BindConvert bindConvert, ParameterInfo parameterInfo, 
                IApplication httpApp, HttpRequestMessage request,
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
                return await onFailure(ex.Message).AsTask();
            }
        }
    }
}
