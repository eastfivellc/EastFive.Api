using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Api.Resources;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api
{
    public abstract class HttpGenericDelegateAttribute : Attribute, IDocumentResponse, IInstigatableGeneric
    {
        public interface IDefineInstigateMethod
        {

        }

        public class InstigateMethodAttribute : Attribute, IDefineInstigateMethod
        {
        }

        public virtual HttpStatusCode StatusCode { get; set; }

        public virtual string Example { get; set; }

        protected Type type;
        protected HttpApplication httpApp;
        protected HttpRequestMessage request;
        protected ParameterInfo parameterInfo;

        public virtual Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var response = new Response()
            {
                Name = paramInfo.Name,
                StatusCode = this.StatusCode,
                Example = this.Example,
                Headers = new KeyValuePair<string, string>[] { },
            };
            return paramInfo
                .GetAttributesInterface<IModifyDocumentResponse>()
                .Aggregate(response,
                    (last, attr) => attr.GetResponse(last, paramInfo, httpApp));
        }

        public virtual Task<HttpResponseMessage> InstigatorDelegateGeneric(Type type,
            HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
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

            return attrType
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
    }
}
