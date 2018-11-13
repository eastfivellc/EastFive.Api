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

    public struct SelectParameterResult
    {
        public static SelectParameterResult Failure(string why, string key, ParameterInfo parameterInfo)
        {
            return new SelectParameterResult
            {
                valid = false,
                value = default(object),
                failure = why,
                parameterInfo = parameterInfo,
                fromQuery = true,
                fromBody = false,
                key = key,
            };
        }

        public static SelectParameterResult Body(object v, string key, ParameterInfo parameterInfo)
        {
            return new SelectParameterResult
            {
                valid = true,
                value = v,
                failure = string.Empty,
                parameterInfo = parameterInfo,
                fromQuery = false,
                fromBody = true,
                key = key,
            };
        }

        public SelectParameterResult(ParameterInfo parameterInfo, string why, string key)
        {
            valid = false;
            value = default(object);
            failure = why;
            this.parameterInfo = parameterInfo;
            fromQuery = true;
            fromBody = false;
            this.key = key;
        }

        public SelectParameterResult(object v, string key, ParameterInfo parameterInfo)
        {
            valid = true;
            value = v;
            failure = string.Empty;
            this.parameterInfo = parameterInfo;
            fromQuery = true;
            fromBody = false;
            this.key = key;
        }

        public bool valid;
        public object value;
        public string failure;
        public ParameterInfo parameterInfo;
        public bool fromQuery;
        public bool fromBody;
        public string key;
    }

    public delegate Task<TResult> CastDelegate<TResult>(string query, Type type,
        Func<object, TResult> onCasted,
        Func<string, TResult> onFailedToCast);

    public interface IProvideApiValue
    {
        Task<SelectParameterResult> TryCastAsync(HttpApplication httpApp, HttpRequestMessage request,
                MethodInfo method, ParameterInfo parameterRequiringValidation,
                CastDelegate<SelectParameterResult> fetchQueryParam,
                CastDelegate<SelectParameterResult> fetchBodyParam,
                CastDelegate<SelectParameterResult> fetchDefaultParam);
    }
}
