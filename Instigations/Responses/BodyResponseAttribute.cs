using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class BodyResponseAttribute : HttpFuncDelegateAttribute
    {
        public override Task<HttpResponseMessage> InstigateInternal(IApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ContentResponse responseDelegate =
                (obj, contentType) =>
                {
                    var response = GetResponse(obj, contentType,
                        httpApp, request, parameterInfo);
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess(responseDelegate);
        }

        private HttpResponseMessage GetResponse(object obj, string contentType,
            IApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo)
        {
            var objType = obj.GetType();
            if (!objType.ContainsAttributeInterface<IProvideSerialization>())
            {
                var response = request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
                if (!contentType.IsNullOrWhiteSpace())
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                return response;
            }

            var responseNoContent = request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
            var serializationProvider = objType.GetAttributesInterface<IProvideSerialization>().Single();
            var customResponse = serializationProvider.Serialize(
                responseNoContent, httpApp, request, parameterInfo, obj);
            return customResponse;
        }
    }
}

