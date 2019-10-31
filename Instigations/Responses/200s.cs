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

using RazorEngine.Templating;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using Microsoft.ApplicationInsights.DataContracts;

namespace EastFive.Api
{
    [ContentTypeResponse(StatusCode = HttpStatusCode.OK)]
    public delegate HttpResponseMessage ContentTypeResponse<TResource>(object content, string contentType = default(string));
    public class ContentTypeResponseAttribute : HttpGenericDelegateAttribute
    {
        public override string Example => "serialized object";

        [InstigateMethod]
        public HttpResponseMessage ContentResponse<TResource>(object content, string contentTypeString = default(string))
        {
            Type GetType(Type type)
            {
                if (type.IsArray)
                    return GetType(type.GetElementType());
                return type;
            }

            return GetType(typeof(TResource))
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
        }
    }

    [ContentResponse(StatusCode = System.Net.HttpStatusCode.OK)]
    public delegate HttpResponseMessage ContentResponse(object content, string contentType = default(string));
    public class ContentResponseAttribute : HttpFuncDelegateAttribute
    {
        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request,ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ContentResponse responseDelegate =
                (obj, contentType) =>
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
                };
            return onSuccess(responseDelegate);
        }
    }

    #region Data

    [BytesResponse]
    public delegate HttpResponseMessage BytesResponse(byte[] bytes, string filename = default, string contentType = default, bool? inline = default);
    public class BytesResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            BytesResponse responseDelegate = (bytes, filename, contentType, inline) =>
            {
                var response = request.CreateResponse(HttpStatusCode.OK);
                response.Content = new ByteArrayContent(bytes);
                if (contentType.HasBlackSpace())
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                if (inline.HasValue)
                    response.Content.Headers.ContentDisposition =
                        new ContentDispositionHeaderValue(inline.Value ? "inline" : "attachment")
                        {
                            FileName = filename,
                        };
                return response;
            };
            return onSuccess(responseDelegate);
        }
    }

    [ImageResponse]
    public delegate HttpResponseMessage ImageResponse(byte[] bytes,
        int? width, int? height, bool? fill,
        string filename = default, string contentType = default);
    public class ImageResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ImageResponse responseDelegate = (imageData, width, height, fill,
                            filename, contentType) =>
            {
                if (width.HasValue || height.HasValue || fill.HasValue)
                {
                    var image = System.Drawing.Image.FromStream(new MemoryStream(imageData));
                    return request.CreateImageResponse(image, width, height, fill, filename);
                }
                var response = request.CreateResponse(HttpStatusCode.OK);
                response.Content = new ByteArrayContent(imageData);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(String.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType);
                return response;
            };
            return onSuccess((object)responseDelegate);
        }
    }


    [PdfResponse()]
    public delegate HttpResponseMessage PdfResponse(byte[] content, string name, bool inline);
    public class PdfResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "PDF File data";

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            PdfResponse responseDelegate = (pdfData, filename, inline) =>
            {
                var response = request.CreatePdfResponse(pdfData, filename, inline);
                return response;
            };
            return onSuccess((object)responseDelegate);
        }
    }

    #endregion

    #region HTML

    [HtmlResponse]
    public delegate HttpResponseMessage HtmlResponse(string content);
    public class HtmlResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "<html><head></head><body>Hello World</body></html>";

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            HtmlResponse responseDelegate = (html) =>
            {
                var response = request.CreateHtmlResponse(html);
                return response;
            };
            return onSuccess((object)responseDelegate);
        }
    }

    [ViewFileResponse]
    public delegate HttpResponseMessage ViewFileResponse(string viewPath, object content);
    public class ViewFileResponseAttribute : HtmlResponseAttribute
    {
        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ViewFileResponse responseDelegate =
                (filePath, content) =>
                {
                    try
                    {
                        var parsedView = RazorEngine.Engine.Razor.RunCompile(filePath, null, content);
                        return request.CreateHtmlResponse(parsedView);
                    }
                    catch (RazorEngine.Templating.TemplateCompilationException ex)
                    {
                        var body = ex.CompilerErrors.Select(error => error.ErrorText).Join(";\n\n");
                        return request.CreateHtmlResponse(body);
                    }
                    catch (Exception ex)
                    {
                        var body = $"Could not load template {filePath} due to:[{ex.GetType().FullName}] `{ex.Message}`";
                        return request.CreateHtmlResponse(body);
                    }
                };
            return onSuccess(responseDelegate);
        }
    }

    [ViewStringResponse]
    public delegate HttpResponseMessage ViewStringResponse(string view, object content);
    public class ViewStringResponseAttribute : HtmlResponseAttribute
    {
        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ViewStringResponse responseDelegate =
                (razorTemplate, content) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.OK);
                    var parsedView = RazorEngine.Razor.Parse(razorTemplate, content);
                    response.Content = new StringContent(parsedView);
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
                    return response;
                };
            return onSuccess(responseDelegate);
        }
    }

    [ViewRenderer()]
    public delegate string ViewRenderer(string filePath, object content);
    public class ViewRendererAttribute : HtmlResponseAttribute
    {
        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ViewRenderer responseDelegate =
                (filePath, content) =>
                {
                    try
                    {
                        var parsedView = RazorEngine.Engine.Razor.RunCompile(filePath, null, content);
                        return parsedView;
                    }
                    catch (RazorEngine.Templating.TemplateCompilationException ex)
                    {
                        var body = ex.CompilerErrors.Select(error => error.ErrorText).Join(";\n\n");
                        return body;
                    }
                    catch (Exception ex)
                    {
                        return $"Could not load template {filePath} due to:[{ex.GetType().FullName}] `{ex.Message}`";
                    }
                };
            return onSuccess(responseDelegate);
        }
    }

    [ViewPath]
    public delegate string ViewPathResolver(string view);
    public class ViewPathAttribute : HtmlResponseAttribute
    {
        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ViewPathResolver responseDelegate =
                (viewPath) =>
                {
                    return $"{System.Web.HttpRuntime.AppDomainAppPath}Views\\{viewPath}";
                };
            return onSuccess(responseDelegate);
        }
    }

    #endregion

    [XlsxResponse()]
    public delegate HttpResponseMessage XlsxResponse(byte[] content, string name);
    public class XlsxResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "<xml></xml>";

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            XlsxResponse responseDelegate = (xlsxData, filename) =>
            {
                var response = request.CreateXlsxResponse(xlsxData, filename);
                return response;
            };
            return onSuccess((object)responseDelegate);
        }
    }

    #region Multipart

    [MultipartResponseAsync]
    public delegate Task<HttpResponseMessage> MultipartResponseAsync(IEnumerable<HttpResponseMessage> responses);
    public class MultipartResponseAsyncAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            MultipartResponseAsync responseDelegate =
                (responses) => request.CreateMultipartResponseAsync(responses);
            return onSuccess((object)responseDelegate);
        }
    }

    [MultipartAcceptArrayResponseAsync]
    public delegate Task<HttpResponseMessage> MultipartAcceptArrayResponseAsync(IEnumerable<object> responses);
    public class MultipartAcceptArrayResponseAsyncAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            MultipartAcceptArrayResponseAsync responseDelegate =
                (objects) =>
                {
                    if (request.Headers.Accept.Contains(accept => accept.MediaType.ToLower().Contains("xlsx")))
                    {
                        return request.CreateMultisheetXlsxResponse(
                            new Dictionary<string, string>(),
                            objects.Cast<BlackBarLabs.Api.ResourceBase>()).AsTask();
                    }
                    var responses = objects.Select(obj => request.CreateResponse(System.Net.HttpStatusCode.OK, obj));
                    return request.CreateMultipartResponseAsync(responses);
                };
            return onSuccess(responseDelegate);
        }
    }

    [MultipartResponseAsyncGeneric]
    public delegate Task<HttpResponseMessage> MultipartResponseAsync<TResource>(IEnumerableAsync<TResource> responses);
    public class MultipartResponseAsyncGenericAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        [InstigateMethod]
        public async Task<HttpResponseMessage> MultipartResponseAsync<T>(IEnumerableAsync<T> objectsAsync)
        {
            if (request.Headers.Accept.Contains(accept => accept.MediaType.ToLower().Contains("xlsx")))
            {
                var objects = await objectsAsync.ToArrayAsync();
                return await request.CreateMultisheetXlsxResponse(
                    new Dictionary<string, string>(),
                    objects.Cast<BlackBarLabs.Api.ResourceBase>()).AsTask();
            }

            var responses = await objectsAsync
                .Select(
                    obj =>
                    {
                        var objType = obj.GetType();
                        if (!objType.ContainsAttributeInterface<IProvideSerialization>())
                        {
                            var response = request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
                            return response;
                        }

                        var responseNoContent = request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
                        var serializationProvider = objType.GetAttributesInterface<IProvideSerialization>().Single();
                        var customResponse = serializationProvider.Serialize(responseNoContent, httpApp, request, parameterInfo, obj);
                        return customResponse;
                    })
                .Async();

            bool IsMultipart()
            {
                var acceptHeader = request.Headers.Accept;
                if (acceptHeader.IsDefaultOrNull())
                    return false;
                if (request.Headers.Accept.Count == 0)
                {
                    var hasMultipart = acceptHeader.ToString().ToLower().Contains("multipart");
                    return hasMultipart;
                }
                return false;
            }

            if (!IsMultipart())
            {
                var jsonStrings = await responses
                    .Select(v => v.Content.ReadAsStringAsync())
                    .AsyncEnumerable()
                    .ToArrayAsync();
                var jsonArrayContent = $"[{jsonStrings.Join(",")}]";
                var response = request.CreateResponse(HttpStatusCode.OK);
                response.Content = new StringContent(jsonArrayContent, Encoding.UTF8, "application/json");
                return response;
            }

            return await request.CreateMultipartResponseAsync(responses);
        }
    }

    #endregion
}
