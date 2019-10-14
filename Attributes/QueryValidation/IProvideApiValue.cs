using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EastFive.Api
{
    public delegate Task<TResult> CastDelegate<TResult>(ParameterInfo parameterInfo,
            // IApplication httpApp, HttpRequestMessage request,
        Func<object, TResult> onCasted,
        Func<string, TResult> onFailedToCast);

    public interface IBindApiValue
    {
        string GetKey(ParameterInfo paramInfo);

        Task<SelectParameterResult> TryCastAsync(IApplication httpApp, HttpRequestMessage request,
            MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate<SelectParameterResult> fetchQueryParam,
            CastDelegate<SelectParameterResult> fetchBodyParam,
            CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters);
    }

    public interface IBindXmlApiValue
    {
        Task<TResult> ParseContentDelegateAsync<TResult>(
            XmlDocument xmlDoc,
            ParameterInfo parameterInfo,
            IApplication httpApp, HttpRequestMessage request,
        Func<object, TResult> onParsed,
        Func<string, TResult> onFailure);
    }

    public interface IBindJsonApiValue
    {
        Task<TResult> ParseContentDelegateAsync<TResult>(Newtonsoft.Json.Linq.JObject contentJObject,
                string contentString, Serialization.BindConvert bindConvert,
            ParameterInfo parameterInfo,
            IApplication httpApp, HttpRequestMessage request,
        Func<object, TResult> onParsed,
        Func<string, TResult> onFailure);
    }

    public interface IProvideApiValue
    {
        string PropertyName { get; }
    }

    
}
