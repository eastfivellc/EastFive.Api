﻿using EastFive.Linq;
using EastFive.Reflection;
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
            return parameter
                .GetAttributesInterface<IBindApiParameter<TProvider>>()
                .First(
                    (paramBinder, next) =>
                    {
                        return paramBinder.Bind(parameter, provider,
                                application,
                            onParsed,
                            (why) =>
                            {
                                return next();
                            },
                            (why) => onFailureToBind(why));
                    },
                    () =>
                    {
                        return application.GetType()
                            .GetAttributesInterface<IBindApiParameter<TProvider>>(true)
                            .First(
                                (paramBinder, next) =>
                                {
                                    return paramBinder.Bind(parameter, provider,
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
                                    return application.Bind(provider, parameter.ParameterType,
                                                onParsed,
                                                onFailureToBind);
                                });
                    });
        }

        public static TResult Bind<TProvider, TResult>(this IApplication application,
                TProvider provider, MemberInfo propertyOrFieldInfo,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailureToBind)
        {
            return propertyOrFieldInfo
                .GetAttributesInterface<IBindApiPropertyOrField<TProvider>>()
                .First(
                    (propBinder, next) =>
                    {
                        return propBinder.Bind<TResult>(propertyOrFieldInfo, provider,
                                application,
                            onParsed,
                            (why) =>
                            {
                                return next();
                            },
                            (why) => onFailureToBind(why));
                    },
                    () =>
                    {
                        return application.GetType()
                            .GetAttributesInterface<IBindApiPropertyOrField<TProvider>>(true)
                            .First(
                                (paramBinder, next) =>
                                {
                                    return paramBinder.Bind(propertyOrFieldInfo, provider,
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
                                    var type = propertyOrFieldInfo.GetPropertyOrFieldType();
                                    return application.Bind(provider, type,
                                                onParsed,
                                                onFailureToBind);
                                });
                    });
        }

        public static TResult Bind<TProvider, TResult>(this IApplication application,
                TProvider provider, Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailureToBind)
        {
            return application.GetType()
                .GetAttributesInterface<IBindParameter<TProvider>>(true)
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
                    () => onFailureToBind($"{application.GetType().FullName} does not have binding attribute that can bind {type.FullName}"));

            //() =>
            //{
            //    return type.Bind(provider,
            //            application,
            //        onParsed,
            //        onFailureToBind);
            //});
            //() => onFailureToBind($"{application.GetType().FullName} does not have binding attribute that can bind {type.FullName}"));
        }
    }
}
