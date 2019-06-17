using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<TResult> CastDelegate<TResult>(string query, Type type,
        Func<object, TResult> onCasted,
        Func<string, TResult> onFailedToCast);

    public interface IBindApiValue
    {
        Task<SelectParameterResult> TryCastAsync(IApplication httpApp, HttpRequestMessage request,
                MethodInfo method, ParameterInfo parameterRequiringValidation,
                CastDelegate<SelectParameterResult> fetchQueryParam,
                CastDelegate<SelectParameterResult> fetchBodyParam,
                CastDelegate<SelectParameterResult> fetchDefaultParam);
    }

    public interface IProvideApiValue
    {
        string PropertyName { get; }
    }

    
}
