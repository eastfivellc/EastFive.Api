using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using BlackBarLabs.Extensions;
using BlackBarLabs.Api.Resources;
using System.Security.Claims;
using System.Configuration;
using EastFive.Api;
using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Web;
using EastFive.Linq.Async;
using EastFive.Web.Configuration;
using Microsoft.AspNetCore.Http;
using EastFive.Api.Core;

namespace EastFive.Api
{
    public static class RequestResponseExtensions
    {

        public static async Task<IHttpResponse> CreateMultipartResponseAsync(this IHttpRequest request,
            IEnumerable<IHttpRequest> contents)
        {
            if (request.TryGetMediaType(out string mediaType))
            {
                if (mediaType.ToLower().Contains("multipart/mixed"))
                    return request.CreateHttpMultipartResponse(contents);

                if (mediaType.ToLower().Contains("application/json+content-array"))
                    return await request.CreateJsonArrayResponseAsync(contents);
            }
            return await request.CreateBrowserMultipartResponse(contents);

        }

        private static IHttpResponse CreateHttpMultipartResponse(this IHttpRequest request,
            IEnumerable<IHttpRequest> contents)
        {
            var multipartContent = new MultipartContent("mixed", "----Boundary_" + Guid.NewGuid().ToString("N"));
            request.CreateResponse(HttpStatusCode.OK, multipartContent);
            throw new NotImplementedException();
            //foreach (var content in contents)
            //{
            //    multipartContent.Add(new HttpMessageContent(content));
            //}
            //var response = request.CreateResponse(HttpStatusCode.OK);
            //response.Content = multipartContent;
            //return response;
        }

        private static async Task<IHttpResponse> CreateJsonArrayResponseAsync(this IHttpRequest request,
            IEnumerable<IHttpRequest> contents)
        {
            throw new NotImplementedException();
            //var multipartContent = await contents
            //    .NullToEmpty()
            //    .Select(
            //        async (content) =>
            //        {
            //            return await content.Content.HasValue(
            //                async (contentContent) => await contentContent.ReadAsStringAsync(),
            //                () => content.ReasonPhrase.AsTask());
            //        })
            //    .Parallel()
            //    .ToArrayAsync();

            //var multipartResponse = request.CreateHtmlResponse($"[{multipartContent.Join(',')}]");
            //multipartResponse.Content.Headers.ContentType =
            //    new MediaTypeHeaderValue("application/json+content-array");
            //return multipartResponse;
        }

        private static async Task<IHttpResponse> CreateBrowserMultipartResponse(this IHttpRequest request,
            IEnumerable<IHttpRequest> contents)
        {
            throw new NotImplementedException();
            //var multipartContentTasks = contents.NullToEmpty().Select(
            //    async (content) =>
            //    {
            //        return await content.Content.HasValue(
            //            async (contentContent) =>
            //            {
            //                var response = new Response
            //                {
            //                    StatusCode = content.StatusCode,
            //                    ContentType = contentContent.Headers.ContentType,
            //                    ContentLocation = contentContent.Headers.ContentLocation,
            //                    Content = await contentContent.ReadAsStringAsync(),
            //                    ReasonPhrase = content.ReasonPhrase,
            //                    Location = content.Headers.Location,
            //                };
            //                return response;
            //            },
            //            () =>
            //            {
            //                var response = new Response
            //                {
            //                    StatusCode = content.StatusCode,
            //                    ReasonPhrase = content.ReasonPhrase,
            //                    Location = content.Headers.Location,
            //                };
            //                return Task.FromResult(response);
            //            });
            //    });

            //var multipartContents = await Task.WhenAll(multipartContentTasks);
            //var multipartResponseContent = new MultipartResponse
            //{
            //    StatusCode = HttpStatusCode.OK,
            //    Content = multipartContents,
            //    Location = request.RequestUri,
            //};

            //var multipartResponse = request.CreateResponse(HttpStatusCode.OK, multipartResponseContent);
            //multipartResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-multipart+json");
            //return multipartResponse;
        }

    }
}
