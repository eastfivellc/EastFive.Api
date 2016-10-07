using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using BlackBarLabs.Core;

namespace BlackBarLabs.Api
{
    public static class ResponseExtensions
    {
        public static HttpResponseMessage AddReason(this HttpResponseMessage response, string reason)
        {
            response.ReasonPhrase = reason;
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
    }
}
