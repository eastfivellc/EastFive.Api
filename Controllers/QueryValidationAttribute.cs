using BlackBarLabs.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class QueryValidationAttribute : System.Attribute
    {
        public delegate Task<object> CastDelegate(Type type,
            Func<object, object> onCasted,
            Func<string, object> onFailedToCast);

        internal async Task<TResult> TryCastAsync<TResult>(HttpApplication httpApp, HttpRequestMessage request,
                ParameterInfo parameterRequiringValidation, CastDelegate fetch,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            var obj = await fetch(parameterRequiringValidation.ParameterType,
                v => onCasted(v),
                why => onInvalid(why));
            return (TResult)obj;
        }

        internal Task<TResult> OnEmptyValueAsync<TResult>(HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterRequiringValidation,
            Func<object, TResult> onValid,
            Func<TResult> onInvalid)
        {
            return onInvalid().ToTask();
        }
    }

    public class RequiredAttribute : QueryValidationAttribute
    {
    }

    public class RequiredAndAvailableInPath : QueryValidationAttribute
    {
    }

    
}
