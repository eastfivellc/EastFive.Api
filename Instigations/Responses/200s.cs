using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Images;

namespace EastFive.Api
{
    #region Objects

    [BodyTypeResponse(StatusCode = HttpStatusCode.OK)]
    public delegate IHttpResponse ContentTypeResponse<TResource>(TResource content, string contentType = default(string));
    
    [BodyResponse(StatusCode = System.Net.HttpStatusCode.OK)]
    public delegate IHttpResponse ContentResponse(object content, string contentType = default(string));

    #endregion

    #region Data

    [BytesResponse]
    public delegate IHttpResponse BytesResponse(byte[] bytes, string filename = default, string contentType = default, bool? inline = default);
    public class BytesResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            BytesResponse responseDelegate = (bytes, filename, contentType, inline) =>
            {
                var response = new BytesHttpResponse(request, this.StatusCode,
                    fileName: filename, contentType: contentType, inline: inline,
                    bytes);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [TextResponse]
    public delegate IHttpResponse TextResponse(string content, Encoding encoding = default,
        string filename = default, string contentType = default, bool? inline = default);
    public class TextResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Text";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            TextResponse responseDelegate = (content, encoding, filename, contentType, inline) =>
            {
                var response = new StringHttpResponse(request, this.StatusCode,
                    fileName:filename, contentType:contentType, inline:inline,
                    content, encoding);

                return UpdateResponse(parameterInfo, httpApp, request, response);

            };
            return onSuccess(responseDelegate);
        }
    }

    #region Images

    [ImageRawResponse]
    public delegate IHttpResponse ImageRawResponse(byte[] bytes,
        int? width, int? height, bool? fill,
        string filename = default, string contentType = default);
    public class ImageRawResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ImageRawResponse responseDelegate = (imageData, width, height, fill,
                            filename, contentType) =>
            {
                if (width.HasValue || height.HasValue || fill.HasValue)
                {
                    var image = System.Drawing.Image.FromStream(new MemoryStream(imageData));
                    var resizedResponse = new ImageHttpResponse(request, this.StatusCode,
                        image, width, height, fill, filename);
                    return UpdateResponse(parameterInfo, httpApp, request, resizedResponse);
                }
                var contentTypeFinal = contentType.NullToEmpty()
                    .StartsWith("image", StringComparison.OrdinalIgnoreCase) ?
                        contentType
                        :
                        System.Drawing.Image.FromStream(new MemoryStream(imageData)).GetMimeType();
                var response = new BytesHttpResponse(request, this.StatusCode,
                    filename,
                    contentTypeFinal, 
                    default,
                    imageData);
                //response.SetContentType(contentTypeFinal);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess((object)responseDelegate);
        }

    }


    [ImageResponse]
    public delegate IHttpResponse ImageResponse(Image image,
        int? width = default, int? height = default, bool? fill = default,
        string contentType = default,
        string filename = default);
    public class ImageResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ImageResponse responseDelegate = (image, 
                width, height, fill,
                contentType, filename) =>
            {
                var newImage = image.ResizeImage(width, height, fill);
                var codec = contentType.ParseImageCodecInfo();
                var response = new ImageHttpResponse(newImage, codec, request, this.StatusCode);
                response.SetContentType(codec.MimeType);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess((object)responseDelegate);
        }

        private class ImageHttpResponse : HttpResponse
        {
            public ImageHttpResponse(Image image, ImageCodecInfo codec,
                IHttpRequest request, HttpStatusCode statusCode)
                : base(request, statusCode,
                      async (responseStream) =>
                      {
                          // Hack for bug with images
                          using (var intermediaryStream = new MemoryStream())
                          {
                              image.Save(intermediaryStream, codec,
                                  encoderQuality: 80L);
                              intermediaryStream.Position = 0;
                              await intermediaryStream.CopyToAsync(responseStream);
                              await intermediaryStream.FlushAsync();
                              await responseStream.FlushAsync();
                              //responseStream.WriteAsync(intermediaryStream.ToArray())
                          }
                      })
            {
            }
        }
    }

    [PdfResponse()]
    public delegate IHttpResponse PdfResponse(byte[] content, string name, bool inline);
    public class PdfResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "PDF File data";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            PdfResponse responseDelegate = (pdfData, filename, inline) =>
            {
                var response = new BytesHttpResponse(request, this.StatusCode,
                    fileName: filename, contentType: "application/pdf", inline: inline,
                    data: pdfData);
                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [SvgResponse]
    public delegate IHttpResponse SvgResponse(string content, Encoding encoding = default,
        string filename = default, bool? inline = default);
    public class SvgResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "<svg></svg>";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            SvgResponse responseDelegate = (content, encoding, filename, inline) =>
            {
                var response = new StringHttpResponse(request, this.StatusCode,
                    fileName: filename, contentType: "image/svg+xml", inline: inline,
                    content, encoding);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    #endregion

    #endregion

    #region HTML

    [HtmlResponse]
    public delegate IHttpResponse HtmlResponse(string content);
    public class HtmlResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "<html><head></head><body>Hello World</body></html>";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            HtmlResponse responseDelegate = (html) =>
            {
                var response = new HtmlHttpResponse(request, this.StatusCode, html);
                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [ViewFileResponse]
    public delegate IHttpResponse ViewFileResponse(string viewPath, object content);
    public class ViewFileResponseAttribute : HtmlResponseAttribute
    {
        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ViewFileResponse responseDelegate =
                (filePath, content) =>
                {
                    var viewEngine = request.RazorViewEngine;
                    var httpApiApp = httpApp as IApiApplication;
                    var path = httpApiApp.HostEnvironment.ContentRootPath + Path.DirectorySeparatorChar + "Pages";
                    var viewPath = filePath.Replace('/', Path.DirectorySeparatorChar);
                    var viewEngineResult = viewEngine.GetView(path, filePath, false);

                    if (!viewEngineResult.Success)
                        return request.CreateResponse(HttpStatusCode.InternalServerError, $"Couldn't find view {filePath}");

                    var view = viewEngineResult.View;

                    var viewContext = new ViewContext();
                    viewContext.HttpContext = (request as Core.CoreHttpRequest).request.HttpContext;
                    //viewContext.ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<TModel>(
                    //    new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), 
                    //    new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
                    //{ Model = content };
                    viewContext.ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                        new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                        new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
                    { Model = content };
                    return new ViewHttpResponse(view, viewContext, request, this.StatusCode);

                };
            return onSuccess(responseDelegate);
        }

        protected class ViewHttpResponse : HttpResponse
        {
            public ViewHttpResponse(
                Microsoft.AspNetCore.Mvc.ViewEngines.IView view,
                ViewContext viewContext, IHttpRequest request, HttpStatusCode statusCode)
                : base(request, statusCode,
                      async (responseStream) =>
                      {
                          using (var output = new StreamWriter(responseStream))
                          {
                              viewContext.Writer = output;
                              await view.RenderAsync(viewContext);
                              await output.FlushAsync();
                          }
                      })
            {
            }
        }

    }

    //[ViewStringResponse]
    //public delegate IHttpResponse ViewStringResponse(string view, object content);
    //public class ViewStringResponseAttribute : ViewFileResponseAttribute
    //{
    //    public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
    //            IHttpRequest request, ParameterInfo parameterInfo,
    //        Func<object, Task<IHttpResponse>> onSuccess)
    //    {
    //        ViewFileResponse responseDelegate =
    //            (razorSource, content) =>
    //            {
    //                var viewEngine = request.RazorViewEngine;
    //                var filePath = Path.GetTempFileName();
    //                using (StreamWriter sw = new StreamWriter(filePath))
    //                {
    //                    await sw.WriteAsync(razorSource);
    //                }
    //                var viewEngineResult = viewEngine.(  GetView("~/", filePath, false);

    //                if (!viewEngineResult.Success)
    //                    return request.CreateResponse(HttpStatusCode.InternalServerError, $"Couldn't find view {filePath}");

    //                var view = viewEngineResult.View;

    //                var viewContext = new ViewContext();
    //                viewContext.HttpContext = (request as Core.CoreHttpRequest).request.HttpContext;
    //                //viewContext.ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<TModel>(
    //                //    new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), 
    //                //    new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
    //                //{ Model = content };
    //                viewContext.ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
    //                    new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
    //                    new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
    //                { Model = content };
    //                return new ViewHttpResponse(view, viewContext, request, this.StatusCode);

    //            };
    //        return onSuccess(responseDelegate);
    //    }
    //}

    #endregion

    [XlsxResponse()]
    public delegate IHttpResponse XlsxResponse(byte[] content, string name);
    public class XlsxResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "<xml></xml>";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            XlsxResponse responseDelegate = (xlsxData, filename) =>
            {
                var response = new BytesHttpResponse(request, this.StatusCode,
                    fileName: filename.IsNullOrWhiteSpace() ? $"sheet.xlsx" : filename, 
                    contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
                    inline: false,
                    xlsxData);
                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess((object)responseDelegate);
        }
    }

    #region Multipart

    [MultipartAsyncResponseGeneric]
    public delegate IHttpResponse MultipartAsyncResponse<TResource>(IEnumerableAsync<TResource> responses);
    public class MultipartAsyncResponseGenericAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        [InstigateMethod]
        public IHttpResponse EnumerableAsyncHttpResponse<T>(IEnumerableAsync<T> objectsAsync)
        {
            var response = new EnumerableAsyncHttpResponse<T>(this.httpApp, request, this.parameterInfo,
                this.StatusCode,
                objectsAsync);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }
    }

    [MultipartResponseAsyncGeneric]
    [Obsolete("Use MultipartAsyncResponse instead")]
    public delegate Task<IHttpResponse> MultipartResponseAsync<TResource>(IEnumerableAsync<TResource> responses);
    public class MultipartResponseAsyncGenericAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        [InstigateMethod]
        public async Task<IHttpResponse> EnumerableAsyncHttpResponse<T>(IEnumerableAsync<T> objectsAsync)
        {
            var response = new EnumerableAsyncHttpResponse<T>(this.httpApp, request, this.parameterInfo,
                this.StatusCode,
                objectsAsync);
            return UpdateResponse(parameterInfo, httpApp, request, response);

            //if (request.GetAcceptTypes().Contains(accept => accept.MediaType.ToLower().Contains("xlsx")))
            //{
            //    return await MultipartXlsAsync(objectsAsync);
            //}


            //bool IsMultipart()
            //{
            //    var acceptHeader = request.Headers.Accept;
            //    if (acceptHeader.IsDefaultOrNull())
            //        return false;
            //    if (request.Headers.Accept.Count == 0)
            //    {
            //        var hasMultipart = acceptHeader.ToString().ToLower().Contains("multipart");
            //        return hasMultipart;
            //    }
            //    return false;
            //}

            //if (IsMultipart())
            //{
            //    var response = await request.CreateMultipartResponseAsync(responses);
            //    return UpdateResponse(parameterInfo, httpApp, request, response);
            //}


        }

        private async Task<IHttpResponse> MultipartXlsAsync<T>(IEnumerableAsync<T> objectsAsync)
        {
            var objects = await objectsAsync.ToArrayAsync();
            throw new NotImplementedException();

            //var xlsMultisheet = request.CreateMultisheetXlsxResponse(
            //    new Dictionary<string, string>(),
            //    objects.Cast<IReferenceable>());
            //return UpdateResponse(parameterInfo, httpApp, request, xlsMultisheet);

        }
    }


    //[MultipartResponseAsync]
    //public delegate Task<IHttpResponse> MultipartResponseAsync(IEnumerable<IHttpResponse> responses);
    //public class MultipartResponseAsyncAttribute : HttpFuncDelegateAttribute
    //{
    //    public override HttpStatusCode StatusCode => HttpStatusCode.OK;

    //    public override string Example => "[]";

    //    public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
    //            IHttpRequest request, ParameterInfo parameterInfo,
    //        Func<object, Task<IHttpResponse>> onSuccess)
    //    {
    //        MultipartResponseAsync responseDelegate =
    //            async (responses) =>
    //            {
    //                var response = await request.CreateMultipartResponseAsync(responses);
    //                return UpdateResponse(parameterInfo, httpApp, request, response);
    //            };
    //        return onSuccess(responseDelegate);
    //    }
    //}

    [MultipartAcceptArrayResponse]
    public delegate IHttpResponse MultipartAcceptArrayResponse(IEnumerable<object> responses);
    public class MultipartAcceptArrayResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            MultipartAcceptArrayResponse responseDelegate =
                (objects) =>
                {
                    var objectsArr = objects.ToArray();
                    var response = new JsonHttpResponse(request, this.StatusCode, objectsArr);
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess(responseDelegate);
        }
    }

    [MultipartAcceptArrayResponseType]
    public delegate IHttpResponse MultipartAcceptArrayResponse<TResource>(IEnumerable<TResource> responses);
    public class MultipartAcceptArrayResponseTypeAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        [InstigateMethod]
        public IHttpResponse MultipartAcceptArrayResponse<TResource>(IEnumerable<TResource> responses)
        {
            var objectsArr = responses.ToArray();
            var response = new JsonHttpResponse(request, this.StatusCode, objectsArr);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }

    }

    [Obsolete("Use MultipartAcceptArrayResponse (no Async on end)")]
    [BodyTypeResponse]
    public delegate Task<IHttpResponse> MultipartAcceptArrayResponseAsync(IEnumerable<object> responses);
    public class MultipartAcceptArrayResponseAsyncAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            MultipartAcceptArrayResponseAsync responseDelegate =
                (objects) =>
                {
                    var objectsArr = objects.ToArray();
                    var response = new JsonHttpResponse(request, this.StatusCode, objectsArr);
                    return UpdateResponse(parameterInfo, httpApp, request, response).AsTask();

                    //if (request.Headers.Accept.Contains(accept => accept.MediaType.ToLower().Contains("xlsx")))
                    //{
                    //    var xlsResponse = request.CreateMultisheetXlsxResponse(
                    //        new Dictionary<string, string>(),
                    //        objects.Cast<IReferenceable>());
                    //    return UpdateResponse(parameterInfo, httpApp, request, xlsResponse);
                    //}
                    //var responses = objects.Select(obj => request.CreateResponse(System.Net.HttpStatusCode.OK, obj));
                    //var multipart = await request.CreateMultipartResponseAsync(responses);
                    //return UpdateResponse(parameterInfo, httpApp, request, multipart);
                };
            return onSuccess(responseDelegate);
        }
    }

    #endregion
}
