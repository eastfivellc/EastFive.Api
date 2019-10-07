using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Extensions;

namespace EastFive.Api
{
    public class ResourceAttribute : QueryValidationAttribute, IBindApiValue
    {
        public override Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
            HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate<SelectParameterResult> fetchQueryParam,
            CastDelegate<SelectParameterResult> fetchBodyParam,
            CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
        {
            return fetchBodyParam(string.Empty, parameterRequiringValidation.ParameterType,
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
    }
}
