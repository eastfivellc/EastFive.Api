using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Extensions;

namespace EastFive.Api.Auth
{
    public class RequiredClaimAttribute : Attribute, IValidateHttpRequest
    {
        public Uri ClaimType { get; set; }

        public string ClaimValue { get; set; }

        public RequiredClaimAttribute(string requiredClaimType, string requiredClaimValue)
        {
            this.ClaimType = new Uri(requiredClaimType);
            this.ClaimValue = requiredClaimValue;
        }

        public Task<HttpResponseMessage> ValidateRequest(
            KeyValuePair<ParameterInfo, object>[] parameterSelection,
            MethodInfo method,
            IApplication httpApp,
            HttpRequestMessage request,
            ValidateHttpDelegate boundCallback)
        {
            //if (!request.IsAuthorizedFor(ClaimType, ClaimValue))
            //    return request
            //        .CreateResponse(System.Net.HttpStatusCode.Unauthorized)
            //        .AddReason($"{method.DeclaringType.FullName}..{method.Name} requires claim `{ClaimType}`=`{this.ClaimValue}`")
            //        .AsTask();
            return boundCallback(parameterSelection, method, httpApp, request);
        }
    }
}
