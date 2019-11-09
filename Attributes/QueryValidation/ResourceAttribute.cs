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

        public SelectParameterResult TryCast(IApplication httpApp,
            HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate fetchQueryParam,
            CastDelegate fetchBodyParam,
            CastDelegate fetchDefaultParam)
        {
            return fetchBodyParam(parameterRequiringValidation,
                (value) => SelectParameterResult.Body(value, string.Empty, parameterRequiringValidation),
                (why) => SelectParameterResult.FailureBody(why, string.Empty, parameterRequiringValidation));
        }

        public TResult ParseContentDelegate<TResult>(JObject contentJObject,
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
                return onFailure(ex.Message);
            }
        }
    }
}
