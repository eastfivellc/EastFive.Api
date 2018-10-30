using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface CastResult
    {

    }

    public delegate Task<TResult> CastDelegate<TResult>(string query, Type type,
        Func<object, TResult> onCasted,
        Func<string, TResult> onFailedToCast);

    public interface IProvideApiValue
    {
        Task<TResult> TryCastAsync<TResult>(HttpApplication httpApp, HttpRequestMessage request,
                MethodInfo method, ParameterInfo parameterRequiringValidation,
                CastDelegate<TResult> fetchQueryParam,
                CastDelegate<TResult> fetchBodyParam,
                CastDelegate<TResult> fetchDefaultParam,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid);
    }
}
