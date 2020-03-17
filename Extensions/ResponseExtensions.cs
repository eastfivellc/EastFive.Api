using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Linq;
using BlackBarLabs.Api.Resources;
using BlackBarLabs.Extensions;
using EastFive;
using EastFive.Linq.Expressions;
using EastFive.Sheets;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Api.Controllers;
using EastFive.Api;
using EastFive.Collections.Generic;
using BlackBarLabs.Api;
using Microsoft.AspNetCore.Mvc.Routing;

namespace EastFive.Api
{
    public static class ResponseExtensions
    {
        public static HttpResponseMessage AddReason(this HttpResponseMessage response, string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return response;

            var reasonPhrase = reason.Replace('\n', ';').Replace("\r", "");
            if (reasonPhrase.Length > 510)
                reasonPhrase = new string(reasonPhrase.Take(510).ToArray());
            response.ReasonPhrase = reasonPhrase;

            // TODO: Check user agent and only set this on iOS and other crippled systems
            response.Headers.Add("Reason", reasonPhrase);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(new { Message = reason }));
            return response;
        }
        
        public static HttpResponseMessage CreateContentResponse(this HttpRequestMessage request, byte [] content, string mediaType)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(content);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            return response;
        }

        public static HttpResponseMessage CreateFileResponse(this HttpRequestMessage request, byte [] content, string mediaType,
            bool? inline = default(bool?), string filename = default(string))
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(content);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            if(inline.HasValue)
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(inline.Value ? "inline" : "attachment")
                {
                    FileName =
                            default(string) == filename ?
                                Guid.NewGuid().ToString("N") + ".pdf" :
                                filename,
                };
            return response;
        }

        public static HttpResponseMessage CreatePdfResponse(this HttpRequestMessage request, System.IO.Stream stream,
            string filename = default(string), bool inline = false)
        {
            var result = stream.ToBytes(
                (pdfData) => request.CreatePdfResponse(pdfData, filename, inline));
            return result;
        }

        public static HttpResponseMessage CreatePdfResponse(this HttpRequestMessage request, byte[] pdfData,
            string filename = default(string), bool inline = false)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(pdfData);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment")
            {
                FileName =
                            default(string) == filename ?
                                Guid.NewGuid().ToString("N") + ".pdf" :
                                filename,
            };
            return response;
        }

        public static HttpResponseMessage CreateImageResponse(this HttpRequestMessage request, byte[] imageData,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?),
            string filename = default(string), string contentType = default(string))
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
        }

        public static HttpResponseMessage CreateImageResponse(this HttpRequestMessage request, Image image,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?),
            string filename = default(string))
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            var ratio = ((double)image.Size.Width) / ((double)image.Size.Height);
            var newWidth = (int)Math.Round(width.HasValue ?
                    width.Value
                    :
                    height.HasValue ?
                        height.Value * ratio
                        :
                        image.Size.Width);
            var newHeight = (int)Math.Round(height.HasValue ?
                    height.Value
                    :
                    width.HasValue ?
                        width.Value / ratio
                        :
                        image.Size.Width);

            var newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = Graphics.FromImage(newImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                var brush = System.Drawing.Brushes.White;
                graphics.FillRectangle(brush, 0, 0, newWidth, newHeight);

                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            var encoder = getEncoderInfo("image/jpeg");
            response.Content = new PushStreamContent(
                (outputStream, httpContent, transportContext) =>
                {
                    var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 80L);

                    newImage.Save(outputStream, encoder, encoderParameters);
                    outputStream.Close();
                }, new MediaTypeHeaderValue(encoder.MimeType));
            return response;
        }

        public static HttpResponseMessage CreateImageResponse(this HttpRequestMessage request,
            Image image,
            string filename = default(string),
            string contentType = "image/jpeg")
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            
            var encoder = getEncoderInfo(contentType);
            response.Content = new PushStreamContent(
                (outputStream, httpContent, transportContext) =>
                {
                    var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 80L);

                    image.Save(outputStream, encoder, encoderParameters);
                    outputStream.Close();
                }, new MediaTypeHeaderValue(encoder.MimeType));
            //TODO: response.Content.Headers. = filename
            return response;
        }

        private static ImageCodecInfo getEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();

            for (int j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType.ToLower() == mimeType.ToLower())
                {
                    return encoders[j];
                }
            }

            return null;
        }

        //public static HttpResponseMessage CreateResponsesBackground(this HttpRequestMessage request,
        //        System.Web.Http.Routing.UrlHelper urlHelper,
        //    Func<Func<HttpResponseMessage, BackgroundProgressController.Process>, Task<BackgroundProgressController.Process[]>> callback,
        //    int? estimatedProcessLength = default(int?))
        //{
        //    var processId = BackgroundProgressController.CreateProcess(callback, estimatedProcessLength);
        //    var response = request.CreateResponse(HttpStatusCode.Accepted);
        //    response.Headers.Add("Access-Control-Expose-Headers", "x-backgroundprocess");
        //    response.Headers.Add("x-backgroundprocess", urlHelper.GetLocation<BackgroundProgressController>(processId).AbsoluteUri);
        //    return response;
        //}

        //public static HttpResponseMessage CreateResponseBackground(this HttpRequestMessage request,
        //        System.Web.Http.Routing.UrlHelper urlHelper,
        //    Func<Action<double>, Task<HttpResponseMessage>> callback,
        //    double? estimatedProcessLength = default(double?))
        //{
        //    var processId = BackgroundProgressController.CreateProcess(callback, estimatedProcessLength);
        //    var response = request.CreateResponse(HttpStatusCode.Accepted);
        //    response.Headers.Add("Access-Control-Expose-Headers", "x-backgroundprocess");
        //    response.Headers.Add("x-backgroundprocess", urlHelper.GetLocation<BackgroundProgressController>(processId).AbsoluteUri);
        //    return response;
        //}

        public static HttpResponseMessage CreateResponseVideoStream(this HttpRequestMessage request,
            byte [] video, string contentType)
        {
            var response = request.CreateResponse(HttpStatusCode.PartialContent);
            var ranges =
                (
                    request.Headers.Range.IsDefaultOrNull() ?
                        default(RangeItemHeaderValue[])
                        :
                        request.Headers.Range.Ranges
                )
                .NullToEmpty();
            if (!ranges.Any())
                ranges = new RangeItemHeaderValue[]
                    {
                        new RangeItemHeaderValue(0, video.LongLength-1)
                    };

            response.Content = new PushStreamContent(
                async (outputStream, httpContent, transportContext) =>
                {
                    try
                    {
                        foreach (var range in ranges)
                        {
                            if (!range.From.HasValue)
                                continue;
                            var length = range.To.HasValue ?
                                (range.To.Value - range.From.Value)
                                :
                                (video.LongLength - range.From.Value);
                            await outputStream.WriteAsync(video, (int)range.From.Value, (int)length);
                        }
                    }
                    catch (Exception)
                    {
                        return;
                    }
                    finally
                    {
                        outputStream.Close();
                    }
                }, new MediaTypeHeaderValue(contentType));
            response.Headers.AcceptRanges.Add("bytes");
            response.Headers.CacheControl = new CacheControlHeaderValue() { MaxAge = TimeSpan.FromSeconds(10368000), };
            response.Content.Headers.ContentLength = video.LongLength;
            var rangeFirst = ranges.First();
            var to = rangeFirst.To.HasValue ?
                rangeFirst.To.Value
                :
                video.LongLength;
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(rangeFirst.From.Value, to, video.Length);
            return response;
        }

        public static HttpResponseMessage CreateHtmlResponse(this HttpRequestMessage request, string html)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(html);
            writer.Flush();
            stream.Position = 0;
            response.Content = new StreamContent(stream);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }

        public static HttpResponseMessage CreateRedirectResponse<TController>(this HttpRequestMessage request, UrlHelper url,
            string routeName = null)
        {
            var location = url.GetLocation<TController>(routeName);
            return request.CreateRedirectResponse(location);
        }

        public static HttpResponseMessage CreateRedirectResponse(this HttpRequestMessage request, Uri location,
            string routeName = null)
        {
            var response = request
                        .CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Location = location;
            return response;
        }

        public static HttpResponseMessage CreateResponseSeeOther<TController>(this HttpRequestMessage request, Guid otherResourceId, UrlHelper url,
            string routeName = null)
        {
            var location = url.GetLocation<TController>(otherResourceId, routeName);
            var response = request
                        .CreateResponse(HttpStatusCode.SeeOther);
            response.Headers.Location = location;
            return response;
        }
        
        public static HttpResponseMessage CreateAlreadyExistsResponse<TController>(this HttpRequestMessage request, Guid existingResourceId, UrlHelper url,
            string routeName = null)
        {
            var location = url.GetLocation<TController>(existingResourceId, routeName);
            var reason = $"There is already a resource with ID = [{existingResourceId}]";
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict)
                        .AddReason(reason);
            response.Headers.Location = location;
            return response;
        }

        public static HttpResponseMessage CreateAlreadyExistsResponse(this HttpRequestMessage request, Type controllerType, Guid existingResourceId, UrlHelper url,
            string routeName = null)
        {
            var location = url.GetLocation(controllerType, existingResourceId, routeName);
            var reason = $"There is already a resource with ID = [{existingResourceId}]";
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict)
                        .AddReason(reason);
            response.Headers.Location = location;
            return response;
        }

        public static HttpResponseMessage CreateResponseNotFound(this HttpRequestMessage request, Guid resourceId)
        {
            var reason = $"The resource with ID = [{resourceId}] was not found";
            var response = request
                .CreateResponse(HttpStatusCode.NotFound)
                .AddReason(reason);
            return response;
        }

        public static HttpResponseMessage CreateResponseConfiguration(this HttpRequestMessage request, string configParameterName, string why)
        {
            var response = request
                .CreateResponse(HttpStatusCode.ServiceUnavailable)
                .AddReason(why);
            return response;
        }
        
        public static HttpResponseMessage CreateResponseUnexpectedFailure(this HttpRequestMessage request, string why)
        {
            var response = request
                .CreateResponse(HttpStatusCode.InternalServerError)
                .AddReason(why);
            return response;
        }

        public static HttpResponseMessage CreateResponseEmptyId<TQuery, TProperty>(this HttpRequestMessage request,
            TQuery query, Expression<Func<TQuery, TProperty>> propertyFailing)
        {
            var value = string.Empty;
            var reason = $"Property [{propertyFailing}] must have value.";
            var response = request
                .CreateResponse(HttpStatusCode.BadRequest)
                .AddReason(reason);
            return response;
        }

        public static HttpResponseMessage CreateResponseValidationFailure<TQuery, TProperty>(this HttpRequestMessage request, 
            TQuery query, Expression<Func<TQuery, TProperty>> propertyFailing)
        {
            var value = string.Empty;
            try
            {
                value = propertyFailing.Compile().Invoke(query).ToString();
            } catch (Exception)
            {

            }
            var reason = $"Property [{propertyFailing}] Value = [{value}] is not valid";
            var response = request
                .CreateResponse(HttpStatusCode.BadRequest)
                .AddReason(reason);
            return response;
        }

        /// <summary>
        /// The resource could not be created or updated due to a link to a resource that no longer exists.
        /// </summary>
        /// <typeparam name="TController"></typeparam>
        /// <param name="request"></param>
        /// <param name="brokenResourceId"></param>
        /// <param name="url"></param>
        /// <param name="routeName"></param>
        /// <returns></returns>
        public static HttpResponseMessage CreateBrokenReferenceResponse<TController>(this HttpRequestMessage request,
            Guid? brokenResourceId, UrlHelper url,
            string routeName = null)
        {
            var reference = url.GetWebId<TController>(brokenResourceId, routeName);
            var reason = $"The resource with ID = [{brokenResourceId}] at [{reference.Source}] is not available";
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict, reference)
                        .AddReason(reason);
            return response;
        }

        /// <summary>
        /// A query parameter makes reference to a secondary resource that does not exist.
        /// </summary>
        /// <example>/Foo?bar=ABC where the resource referenced by parameter bar does not exist</example>
        /// <typeparam name="TController">The Controller that represents the parameter linked resource</typeparam>
        /// <param name="request"></param>
        /// <param name="brokenResourceProperty"></param>
        /// <param name="url"></param>
        /// <param name="routeName"></param>
        /// <returns></returns>
        public static HttpResponseMessage[] CreateLinkedDocumentNotFoundResponse<TController, TQueryResource>(this HttpRequestMessage request,
            TQueryResource query,
            Expression<Func<TQueryResource, WebIdQuery>> brokenResourceProperty,
            UrlHelper url,
            string routeName = null)
        {
            var reference = default(WebIdQuery);
            try
            {
                reference = brokenResourceProperty.Compile().Invoke(query);
            } catch(Exception)
            {

            }

            var reason = reference.IsDefault()?
                $"The referenced [{typeof(TController).Name}] resource is not found"
                :
                $"The resource with ID = [{reference.UUIDs}] at [{reference.Source}] is not available";
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict, reference)
                        .AddReason(reason);
            return response.AsEnumerable().ToArray();
        }

        public static HttpResponseMessage[] CreateLinkedDocumentNotFoundResponse<TController, TQueryResource>(this HttpRequestMessage request,
            Guid value,
            Expression<Func<TQueryResource, WebIdQuery>> brokenResourceProperty,
            UrlHelper url,
            string routeName = null)
        {
            var reason = brokenResourceProperty.PropertyName(
                propertyName => $"The referenced [{typeof(TController).Name}] resource [Property({propertyName}) = {value}] is not found",
                () => $"The referenced {typeof(TController).Name} resource = [{value}] is not found");
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict)
                        .AddReason(reason);
            return response.AsEnumerable().ToArray();
        }

        /// <summary>
        /// WARNING: This isn't really baked
        /// </summary>
        /// <param name="viewName"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static HttpResponseMessage CreateResponseHtml(string viewName, dynamic model)
        {
            // var view = File.ReadAllText(Path.Combine(viewDirectory, viewName + ".cshtml"));
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            var template = new RazorEngine.Templating.NameOnlyTemplateKey(viewName, RazorEngine.Templating.ResolveType.Global, null);
            var parsedView = RazorEngine.Engine.Razor.Run(template, model.GetType(), model);
            response.Content = new StringContent(parsedView);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }
    }
}