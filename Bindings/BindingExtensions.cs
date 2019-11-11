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
        public static TResult Bind<TProvider, TResult>(this IApplication application,
                TProvider provider, ParameterInfo parameter,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailureToBind)
        {
            return application.GetType()
                .GetAttributesInterface<IBindApiParameter<TProvider>>(true)
                .First(
                    (paramBinder, next) =>
                    {
                        return paramBinder.Bind(parameter.ParameterType, provider,
                            onParsed,
                            (why) =>
                            {
                                return parameter.ParameterType.Bind(provider,
                                    onParsed,
                                    onFailureToBind);
                            },
                            onFailureToBind);
                    },
                    () =>
                    {
                        return parameter.Bind(provider,
                                    onParsed,
                                    onFailureToBind);
                    });
        }

        public static TResult Bind<TProvider, TResult>(this IApplication application,
                TProvider provider, Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailureToBind)
        {
            return application.GetType()
                .GetAttributesInterface<IBindApiParameter<TProvider>>(true)
                .First(
                    (paramBinder, next) =>
                    {
                        return paramBinder.Bind(type, provider,
                            onParsed,
                            (why) =>
                            {
                                return type.Bind(provider,
                                    onParsed,
                                    onFailureToBind);
                            },
                            onFailureToBind);
                    },
                    () => onFailureToBind($"{application.GetType().FullName} does not have binding attributes"));
        }

        public static TResult Bind<TProvider, TResult>(this ParameterInfo parameter,
                TProvider provider,
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
                            (why) =>
                            {
                                return parameter.ParameterType.Bind(provider,
                                    onParsed,
                                    onDidNotBind);
                            },
                            onDidNotBind);
                    },
                    () => parameter.ParameterType.Bind(provider,
                        onParsed,
                        onDidNotBind));
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
                            onDidNotBind,
                            onDidNotBind);
                    },
                    () => onDidNotBind($"No bindings from {typeof(TProvider).FullName} => {type.FullName}"));
        }
    }
}
