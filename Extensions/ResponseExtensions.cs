using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace BlackBarLabs.Api
{
    public static class ResponseExtensions
    {
        public static HttpResponseMessage AddReason(this HttpResponseMessage response, string reason)
        {
            var reasonPhrase = reason.Replace('\n', ';').Replace("\r", "");
            response.ReasonPhrase = reasonPhrase;
            // TODO: Check user agent and only set this on iOS and other crippled systems
            response.Headers.Add("Reason", reasonPhrase);
            return response;
        }

        public static HttpResponseMessage CreatePdfResponse(this HttpRequestMessage request, System.IO.Stream stream,
            string filename = default(string), bool inline = false)
        {
            var result = stream.ToBytes(
                (pdfData) => request.CreatePdfResponse(pdfData, filename, inline));
            return result;
        }

        public static HttpResponseMessage CreatePdfResponse(this HttpRequestMessage request, byte [] pdfData,
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

        public static HttpResponseMessage CreateXlsxResponse(this HttpRequestMessage request, Stream xlsxData, string filename = "")
        {
            var content = new StreamContent(xlsxData);
            return request.CreateXlsxResponse(content, filename);
        }

        public static HttpResponseMessage CreateXlsxResponse(this HttpRequestMessage request, byte [] xlsxData, string filename = "")
        {
            var content = new ByteArrayContent(xlsxData);
            return request.CreateXlsxResponse(content, filename);
        }


        public static HttpResponseMessage CreateXlsxResponse(this HttpRequestMessage request, HttpContent xlsxContent, string filename = "")
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = xlsxContent;
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.template");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = String.IsNullOrWhiteSpace(filename) ? $"sheet.xlsx" : filename,
            };
            return response;
        }

        public static HttpResponseMessage CreateImageResponse(this HttpRequestMessage request, byte [] imageData,
            string filename = default(string), string contentType = default(string))
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(imageData);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(String.IsNullOrWhiteSpace(contentType)? "image/png" : contentType);
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

        public static HttpResponseMessage CreateAlreadyExistsResponse<TController>(this HttpRequestMessage request, Guid existingResourceId, System.Web.Http.Routing.UrlHelper url,
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

        public static HttpResponseMessage CreateBrokenReferenceResponse<TController>(this HttpRequestMessage request, Guid? brokenResourceId, System.Web.Http.Routing.UrlHelper url,
            string routeName = null)
        {
            var reference = url.GetWebId<TController>(brokenResourceId, routeName);
            var reason = $"The resource with ID = [{brokenResourceId}] at [{reference.Source}] is not available";
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict, reference)
                        .AddReason(reason);
            return response;
        }
        
    }
}
