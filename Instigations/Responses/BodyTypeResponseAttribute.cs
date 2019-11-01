using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api
{
    public class BodyTypeResponseAttribute : HttpGenericDelegateAttribute
    {
        public override string Example => "serialized object";

        [InstigateMethod]
        public HttpResponseMessage ContentResponse<TResource>(TResource content, string contentTypeString = default(string))
        {
            Type GetType(Type type)
            {
                if (type.IsArray)
                    return GetType(type.GetElementType());
                return type;
            }

            var responseWithContent = GetType(typeof(TResource))
                .GetAttributesInterface<IProvideSerialization>()
                .Select(
                    serializeAttr =>
                    {
                        var quality = request.Headers.Accept
                            .Where(acceptOption => acceptOption.MediaType.ToLower() == serializeAttr.MediaType.ToLower())
                            .First(
                                (acceptOption, next) => acceptOption.Quality.HasValue ? acceptOption.Quality.Value : -1.0,
                                () => -2.0);
                        return serializeAttr.PairWithValue(quality);
                    })
                .OrderByDescending(kvp => kvp.Value)
                .First(
                    (serializerQualityKvp, next) =>
                    {
                        var serializationProvider = serializerQualityKvp.Key;
                        var quality = serializerQualityKvp.Value;
                        var responseNoContent = request.CreateResponse(this.StatusCode, content);
                        var customResponse = serializationProvider.Serialize(responseNoContent, httpApp, request, parameterInfo, content);
                        return customResponse;
                    },
                    () =>
                    {
                        var response = request.CreateResponse(this.StatusCode, content);
                        if (!contentTypeString.IsNullOrWhiteSpace())
                            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentTypeString);
                        return response;
                    });
            return UpdateResponse(parameterInfo, httpApp, request, responseWithContent);
        }
    }
}
