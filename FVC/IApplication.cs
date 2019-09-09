using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<HttpResponseMessage> InstigatorDelegate(
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess);

    public delegate Task<HttpResponseMessage> InstigatorDelegateGeneric(
            Type type, HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
        Func<object, Task<HttpResponseMessage>> onSuccess);

    public delegate Task<TResult> ParseContentDelegateAsync<TResult>(string key, Type type,
        Func<object, TResult> onParsed,
        Func<string, TResult> onFailure);

    public interface IApplication // : IInvokeApplication
    {
        EastFive.Analytics.ILogger Logger { get; }

        IEnumerable<MethodInfo> GetExtensionMethods(Type controllerType);
        object CastResourceProperty(object value, Type propertyType);

        void SetInstigator(Type type, InstigatorDelegate instigator, bool clear = false);

        void SetInstigatorGeneric(Type type, InstigatorDelegateGeneric instigator, bool clear = true);

        TResult GetControllerType<TResult>(string routeName,
            Func<Type, TResult> onMethodsIdentified,
            Func<TResult> onKeyNotFound);

        TResult Bind<TResult>(Type type, Serialization.IParseToken content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind);

        Task<TResult> ParseContentValuesAsync<TParseResult, TResult>(HttpContent content,
            Func<ParseContentDelegateAsync<TParseResult>, string[], Task<TResult>> onParsedContentValues);

        Task<HttpResponseMessage> Instigate(HttpRequestMessage request, ParameterInfo methodParameter,
            Func<object, Task<HttpResponseMessage>> onInstigated);
    }
}
