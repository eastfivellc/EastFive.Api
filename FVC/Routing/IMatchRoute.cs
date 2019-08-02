using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IMatchRoute
    {
        bool IsMethodMatch(MethodInfo method, HttpRequestMessage request, IApplication httpApp);

        Task<RouteMatch> IsRouteMatch(
            MethodInfo method, HttpRequestMessage request, IApplication httpApp,
            IEnumerable<string> bodyKeys, CastDelegate<SelectParameterResult> fetchBodyParam);
    }

    public struct RouteMatch
    {
        public bool isValid;
        public MethodInfo method;
        public string[] extraBodyParams;
        public string[] extraQueryParams;
        public string[] extraFileParams;
        public SelectParameterResult[] failedValidations;
        internal KeyValuePair<ParameterInfo, object>[] parametersWithValues;

        public string ErrorMessage
        {
            get
            {
                var failedValidationErrorMessages = failedValidations
                    .Select(
                        paramResult =>
                        {
                            var validator = paramResult.parameterInfo.GetAttributeInterface<IBindApiValue>();
                            var lookupName = validator.GetKey(paramResult.parameterInfo);
                            var location = paramResult.Location;
                            return $"{lookupName}({location}):{paramResult.failure}";
                        })
                    .ToArray();

                var contentFailedValidations = failedValidationErrorMessages.Any() ?
                    $"Please correct the values for [{failedValidationErrorMessages.Join(",")}]"
                    :
                    "";

                var extraParamMessages = extraQueryParams
                    .NullToEmpty()
                    .Select(extraQueryParam => $"{extraQueryParam}(QUERY)")
                    .Concat(
                        extraBodyParams
                            .NullToEmpty()
                            .Select(extraBodyParam => $"{extraBodyParam}(BODY)"));
                var contentExtraParams = extraParamMessages.Any() ?
                    $"emove parameters [{extraParamMessages.Join(",")}]."
                    :
                    "";

                if (contentFailedValidations.IsNullOrWhiteSpace())
                {
                    if (contentExtraParams.IsNullOrWhiteSpace())
                        return "Query validation failure";

                    return $"R{contentExtraParams}";
                }

                if (contentExtraParams.IsNullOrWhiteSpace())
                    return contentFailedValidations;

                return $"{contentFailedValidations} and r{contentExtraParams}";
            }
        }
    }
}
