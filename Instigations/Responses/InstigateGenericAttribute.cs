using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EastFive.Api.Resources;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api
{
    public abstract class InstigateGenericAttribute : InstigatableResponseAttribute, IInstigatableGeneric
    {
        public interface IDefineInstigateMethod
        {

        }

        public class InstigateMethodAttribute : Attribute, IDefineInstigateMethod
        {
        }

        protected Type type;
        protected HttpApplication httpApp;
        protected HttpRequestMessage request;
        protected ParameterInfo parameterInfo;

        public virtual Task<HttpResponseMessage> InstigatorDelegateGeneric(Type type,
                HttpApplication httpApp, HttpRequestMessage request, 
                CancellationToken cancellationToken, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            var scope = CreateScope(type, httpApp, request, parameterInfo);
            return this.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.ContainsAttributeInterface<IDefineInstigateMethod>())
                .First<MethodInfo, Task<HttpResponseMessage>>(
                    (multipartResponseMethodInfoGeneric, next) =>
                    {
                        var multipartResponseMethodInfoBound = multipartResponseMethodInfoGeneric
                            .MakeGenericMethod(type.GenericTypeArguments);
                        var responseDelegate = Delegate.CreateDelegate(type, scope, multipartResponseMethodInfoBound);
                        return onSuccess(responseDelegate);
                    },
                    () =>
                    {
                        throw new NotImplementedException();
                    });
        }

        protected virtual object CreateScope(Type type, 
            HttpApplication httpApp, HttpRequestMessage request, ParameterInfo paramInfo)
        {
            var attrType = this.GetType();
            var scope = Activator.CreateInstance(attrType);
            attrType
                .GetField("type", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(scope, type);
            attrType
                .GetField("httpApp", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(scope, httpApp);
            attrType
                .GetField("request", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(scope, request);
            attrType
                .GetField("parameterInfo", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(scope, parameterInfo);
            return scope;
        }

    }

}
