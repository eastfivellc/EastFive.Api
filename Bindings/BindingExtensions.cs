using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Bindings
{
    public static class BindingExtensions
    {
        public static TResult Bind<TProvider, TResult>(this ParameterInfo parameter,
                TProvider provider,
            IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind)
        {
            return parameter
                .GetAttributesInterface<IBindApiParameter<TProvider>>()
                .First(
                    (paramBinder, next) =>
                    {
                        return paramBinder.Bind(parameter.ParameterType, provider,
                            onParsed,
                            onDidNotBind);
                    },
                    () => parameter
                        .ParameterType
                        .Bind(provider, application, onParsed, onDidNotBind));
        }

        public static TResult Bind<TProvider, TResult>(this Type type,
                TProvider provider,
            IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind)
        {
            return application.GetType()
                .GetAttributesInterface<IBindApiParameter<TProvider>>(true)
                .First(
                    (paramBinder, next) =>
                    {
                        return paramBinder.Bind(type, provider,
                            onParsed,
                            onDidNotBind);
                    },
                    () => type.Bind(provider, onParsed, onDidNotBind));
        }

        public static TResult Bind<TProvider, TResult>(this Type type,
                TProvider provider,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind)
        {
            return type
                .GetAttributesInterface<IBindApiParameter<TProvider>>()
                .First(
                    (paramBinder, next) =>
                    {
                        return paramBinder.Bind(type, provider,
                            onParsed,
                            onDidNotBind);
                    },
                    () => onDidNotBind($"No bindings from {typeof(TProvider).FullName} => {type.FullName}"));
        }
    }
}
