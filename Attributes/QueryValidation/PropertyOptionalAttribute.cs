using EastFive.Api.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class PropertyOptionalAttribute : PropertyAttribute
    {
        public override SelectParameterResult TryCast(
                IApplication httpApp, HttpRequestMessage request, MethodInfo method,
                ParameterInfo parameterRequiringValidation,
                CastDelegate fetchQueryParam,
                CastDelegate fetchBodyParam,
                CastDelegate fetchDefaultParam)
        {
            var baseValue = base.TryCast(httpApp, request, method,
                parameterRequiringValidation, fetchQueryParam, fetchBodyParam, fetchDefaultParam);
            if (baseValue.valid)
                return baseValue;

            baseValue.valid = true;
            baseValue.fromBody = true;
            baseValue.value = GetValue();
            return baseValue;

            object GetValue()
            {
                var parameterType = parameterRequiringValidation.ParameterType;
                if (parameterType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                {
                    var refType = parameterType.GenericTypeArguments.First();
                    var parameterTypeGeneric = typeof(RefOptional<>).MakeGenericType(new Type[] { refType });
                    return Activator.CreateInstance(parameterTypeGeneric, new object[] { });
                }

                if (parameterType.IsSubClassOfGeneric(typeof(IRefs<>)))
                {
                    var refType = parameterType.GenericTypeArguments.First();
                    var parameterTypeGeneric = typeof(Refs<>).MakeGenericType(new Type[] { refType });
                    var refIds = new Guid[] { };
                    var refIdsLookupType = typeof(Func<,>).MakeGenericType(new Type[] { typeof(Guid), refType });
                    var refIdsLookup = refIdsLookupType.GetDefault();
                    return Activator.CreateInstance(
                        parameterTypeGeneric, new object[] { refIds });
                }

                if (parameterType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
                {
                    var parameterTypeGeneric = typeof(Dictionary<,>).MakeGenericType(parameterType.GenericTypeArguments);
                    return Activator.CreateInstance(parameterTypeGeneric);
                }

                return parameterType.GetDefault();
            }
        }

        public override Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var parameter = base.GetParameter(paramInfo, httpApp);
            parameter.Required = false;
            return parameter;
        }
    }
}
