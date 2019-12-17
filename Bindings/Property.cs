using EastFive.Api.Bindings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [PropertyJsonBinder]
    public struct Property<T>
    {
        public bool specified;
        public T value;
    }

    public class PropertyJsonBinderAttribute : Attribute,
            IBindApiParameter<JToken>
    {
        public TResult Bind<TResult>(Type type, JToken content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            BindTypeDelegate<int,int> d = PropertyJsonBinderAttribute.BindType<int,int>;

            var innerType = type.GetGenericArguments().First();
            return (TResult) //typeof(PropertyJsonBinderAttribute)
                             //.GetMethod("BindType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                d.Method.GetGenericMethodDefinition()
                .MakeGenericMethod(new Type[] { innerType, typeof(TResult) })
                .Invoke(null, new object[] { type, content, onParsed, onDidNotBind, onBindingFailure });
        }

        private delegate TResult BindTypeDelegate<T, TResult>(Type type, JToken content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure);

        public static TResult BindType<T, TResult>(Type type, JToken content, 
            Func<object, TResult> onParsed, 
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            var innerType = typeof(T);
            return StandardJTokenBindingsAttribute.BindDirect(innerType, content,
                innerValue =>
                {
                    var prop = new Property<T>
                    {
                        specified = true,
                        value = (T)innerValue,
                    };
                    return onParsed(prop);
                },
                onDidNotBind,
                onBindingFailure);
        }

        public TResult Bind<TResult>(Type type, JsonReader content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            throw new NotImplementedException();
        }
    }
}
