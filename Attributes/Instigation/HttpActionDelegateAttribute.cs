using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BlackBarLabs.Extensions;
using EastFive.Api.Resources;
using EastFive.Extensions;

namespace EastFive.Api
{
    public interface IModifyDocumentResponse
    {
        Response GetResponse(Response response, ParameterInfo paramInfo, HttpApplication httpApp);
    }

    public abstract class HttpDelegateAttribute : Attribute, IDocumentResponse, IInstigatable
    {
        public virtual HttpStatusCode StatusCode { get; set; }

        public virtual string Example { get; set; }

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

        public abstract Task<HttpResponseMessage> Instigate(HttpApplication httpApp,
            HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess);
    }

    public abstract class HttpActionDelegateAttribute : HttpDelegateAttribute
    {
    }

    public abstract class HttpFuncDelegateAttribute : HttpDelegateAttribute
    {
        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var response = base.GetResponse(paramInfo, httpApp);
            if (paramInfo.ParameterType.IsSubClassOfGeneric(typeof(Controllers.CreatedBodyResponse<>)))
            {
                response.Example = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
                return response;
            }
            if (paramInfo.ParameterType.IsSubClassOfGeneric(typeof(MultipartResponseAsync<>)))
            {
                var typeName = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
                response.Example = $"{typeName}[]";
                return response;
            }
            return response;
        }
    }

    public abstract class HttpHeaderDelegateAttribute : HttpDelegateAttribute
    {
        public string HeaderName { get; set; }
        public string HeaderValue { get; set; }

        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var response = base.GetResponse(paramInfo, httpApp);
            response.Headers = HeaderName.PairWithValue(HeaderValue).AsArray();
            if (paramInfo.ParameterType.IsSubClassOfGeneric(typeof(Controllers.CreatedBodyResponse<>)))
            {
                response.Example = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
                return response;
            }
            if (paramInfo.ParameterType.IsSubClassOfGeneric(typeof(Controllers.CreatedBodyResponse<>)))
            {
                response.Example = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
                return response;
            }
            if (paramInfo.ParameterType.IsSubClassOfGeneric(typeof(MultipartResponseAsync<>)))
            {
                var typeName = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
                response.Example = $"{typeName}[]";
                return response;
            }
            return response;
        }
    }
}
