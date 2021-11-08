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
                                application,
                            onParsed,
                            (why) =>
                            {
                                return next();
                            },
                            (why) => next());
                    },
                    () =>
                    {
                        return parameter.Bind(provider, application,
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
                                application,
                            onParsed,
                            (why) =>
                            {
                                return next(); // type.Bind(provider,
                                        //
                                        //application,
                                    //onParsed,
                                    //onFailureToBind);
                            },
                            onFailureToBind);
                    },
                    () =>
                    {
                        return type.Bind(provider,
                                application,
                            onParsed,
                            onFailureToBind);
                    });
                    //() => onFailureToBind($"{application.GetType().FullName} does not have binding attributes"));
        }

        private static TResult Bind<TProvider, TResult>(this ParameterInfo parameter,
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
                                application,
                            onParsed,
                            (why) =>
                            {
                                return next();
                            },
                            (why) => next());
                    },
                    () => parameter.ParameterType.Bind(provider,
                            application,
                        onParsed,
                        onDidNotBind));
                    //() => onDidNotBind(
                    //    $"`{parameter.ParameterType} {parameter.Name}`" + 
                    //    " does not contain an attribute that implements "+
                    //    nameof(IBindApiParameter<TProvider>)));

        }

        public static TResult Bind<TProvider, TResult>(this Type type,
                TProvider provider,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind)
        {
            return type
                .GetAttributesInterface<IBindApiParameter<TProvider>>()
                .First(
                    (paramBinder, next) =>
                    {
                        return paramBinder.Bind(type, provider,
                                application,
                            onParsed,
                            onDidNotBind,
                            onDidNotBind);
                    },
                    () => onDidNotBind($"No bindings from {typeof(TProvider).FullName} => {type.FullName}"));
        }
    }
}
