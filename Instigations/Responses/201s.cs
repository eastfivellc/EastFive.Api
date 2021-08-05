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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;

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

        private class ProvidedHttpResponse : EastFive.Api.HttpResponse
        {
            public const string DefaultType = "application/json";

            private IProvideSerialization serializationProvider;
            private object obj;

            private IHttpRequest request;
            private IApplication httpApp;
            private ParameterInfo parameterInfo;

            public ProvidedHttpResponse(Type objType, object obj,
                IApplication httpApp, IHttpRequest request, ParameterInfo parameterInfo,
                HttpStatusCode statusCode)
                : base(request, statusCode)
            {
                this.serializationProvider = objType
                    .GetAttributesInterface<IProvideSerialization>()
                    .OrderByDescending(x => x.GetPreference(request))
                    .First(
                        (provider, next) => provider,
                        () => default(IProvideSerialization?));
                this.obj = obj;

                this.request = request;
                this.httpApp = httpApp;
                this.parameterInfo = parameterInfo;
            }

            public override void WriteHeaders(HttpContext context, ResponseHeaders headers)
            {
                base.WriteHeaders(context, headers);
                var contentType = GetContentType();

                headers.ContentType = new Microsoft.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                string GetContentType()
                {
                    if (serializationProvider.IsDefaultOrNull())
                        return DefaultType;

                    var acceptsHeaders = context.Request.GetTypedHeaders().Accept
                        .NullToEmpty();

                    if (UseContentType())
                        return serializationProvider.ContentType;

                    if (UseMediaType())
                        return serializationProvider.MediaType;

                    return DefaultType;

                    bool UseContentType() => acceptsHeaders
                        .Where(
                            accept =>
                            {
                                return accept.MediaType.Equals(
                                    serializationProvider.ContentType,
                                    StringComparison.OrdinalIgnoreCase);
                            })
                        .Any();

                    bool UseMediaType() => acceptsHeaders
                        .Where(
                            accept =>
                            {
                                return accept.MediaType.Equals(
                                    serializationProvider.MediaType,
                                    StringComparison.OrdinalIgnoreCase);
                            })
                        .Any();
                }

            }

            public override Task WriteResponseAsync(Stream stream)
            {
                if (serializationProvider.IsDefaultOrNull())
                    return JsonHttpResponse.WriteResponseAsync(stream, obj, request);
                
                return serializationProvider.SerializeAsync(
                        stream, httpApp, request, parameterInfo, obj);
            }
        }
    }
}
