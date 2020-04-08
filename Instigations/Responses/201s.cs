using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Reflection;
using EastFive.Api.Resources;


namespace EastFive.Api
{
    [StatusCodeResponse(StatusCode = HttpStatusCode.Created)]
    public delegate IHttpResponse CreatedResponse();
    
    [BodyTypeResponse(StatusCode = HttpStatusCode.Created)]
    public delegate IHttpResponse CreatedBodyResponse<TResource>(TResource content, string contentType = default);
    
    public class BodyTypeResponseAttribute : HttpGenericDelegateAttribute
    {
        public override string Example => "serialized object";

        [InstigateMethod]
        public IHttpResponse ContentResponse<TResource>(TResource content, 
            string contentTypeString = default(string))
        {
            var response = new ProvidedHttpResponse(typeof(TResource), content,
                this.httpApp, this.request, this.parameterInfo,
                this.StatusCode);
            return UpdateResponse(parameterInfo, httpApp, request, response);
            //Type GetType(Type type)
            //{
            //    if (type.IsArray)
            //        return GetType(type.GetElementType());
            //    return type;
            //}

            //var responseWithContent = GetType(typeof(TResource))
            //    .GetAttributesInterface<IProvideSerialization>()
            //    .Select(
            //        serializeAttr =>
            //        {
            //            var quality = request.GetAcceptTypes()
            //                .Where(acceptOption => acceptOption.MediaType.ToLower() == serializeAttr.MediaType.ToLower())
            //                .First(
            //                    (acceptOption, next) => acceptOption.Quality.HasValue ? acceptOption.Quality.Value : -1.0,
            //                    () => -2.0);
            //            return serializeAttr.PairWithValue(quality);
            //        })
            //    .OrderByDescending(kvp => kvp.Value)
            //    .First(
            //        (serializerQualityKvp, next) =>
            //        {
            //            var serializationProvider = serializerQualityKvp.Key;
            //            var quality = serializerQualityKvp.Value;
            //            var responseNoContent = request.CreateResponse(this.StatusCode, content);
            //            var customResponse = await serializationProvider.SerializeAsync(responseNoContent, httpApp, request, parameterInfo, content);
            //            customResponse.StatusCode = this.StatusCode;
            //            return customResponse;
            //        },
            //        () =>
            //        {
            //            var response = request.CreateResponse(this.StatusCode, content);
            //            if (!contentTypeString.IsNullOrWhiteSpace())
            //                response.SetContentType(contentTypeString);
            //            return response;
            //        });
            //return UpdateResponse(parameterInfo, httpApp, request, responseWithContent);
        }


        private class ProvidedHttpResponse : HttpResponse
        {
            public ProvidedHttpResponse(Type objType, object obj,
                IApplication httpApp, IHttpRequest request, ParameterInfo parameterInfo,
                HttpStatusCode statusCode)
                : base(request, statusCode,
                    stream =>
                    {
                        var serializationProviders = objType
                            .GetAttributesInterface<IProvideSerialization>()
                            .OrderByDescending(x => x.GetPreference(request));

                        if (serializationProviders.Any())
                        { 
                            var serializationProvider = serializationProviders.First();
                            return serializationProvider.SerializeAsync(
                                stream, httpApp, request, parameterInfo, obj);
                        }

                        return JsonHttpResponse.WriteResponseAsync(stream, obj, request);
                    })
            {

            }
        }
    }
}
