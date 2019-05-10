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
    public class ResourceAttribute : QueryValidationAttribute, IProvideApiValue
    {
        public override Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
            HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate<SelectParameterResult> fetchQueryParam,
            CastDelegate<SelectParameterResult> fetchBodyParam,
            CastDelegate<SelectParameterResult> fetchDefaultParam)
        {
            // TODO: Use more sophisticated method for determining POST resource type (since this can be modified in attributes
            if (method.DeclaringType != parameterRequiringValidation.ParameterType)
                return (new SelectParameterResult
                {
                    fromBody = true,
                    key = "",
                    fromQuery = false,
                    parameterInfo = parameterRequiringValidation,
                    valid = false,
                    failure = $"Inform server developer!!! `{method.DeclaringType.FullName}..{method.Name}: {this.GetType().Name}` attributes a parameter of type `{parameterRequiringValidation.ParameterType.FullName}` on a resource of type `{method.DeclaringType.FullName}`.",
                }).AsTask();
            return fetchBodyParam(string.Empty, parameterRequiringValidation.ParameterType,
                (value) => new SelectParameterResult(value, string.Empty, parameterRequiringValidation),
                (why) => SelectParameterResult.Failure(why, string.Empty, parameterRequiringValidation));
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
