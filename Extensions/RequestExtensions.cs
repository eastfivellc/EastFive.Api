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
using BlackBarLabs.Linq;

namespace BlackBarLabs.Api
{
    public static class RequestExtensions
    {
        public static async Task<IHttpActionResult> GetPossibleMultipartResponseAsync<TResource>(this HttpRequestMessage request,
            IEnumerable<TResource> query,
            Func<TResource, Task<HttpResponseMessage>> singlepart,
            Func<HttpActionDelegate> ifEmpty = default(Func<HttpActionDelegate>))
        {
            if ((!query.Any()) && (!ifEmpty.IsDefaultOrNull()))
            {
                return ifEmpty().ToActionResult();
            }

            var queryTasks = query.Select(resource => singlepart(resource));
            var queryResponses = await Task.WhenAll(queryTasks);
            if (queryResponses.Length == 1)
                return queryResponses[0].ToActionResult();

            return await request.CreateMultipartActionAsync(queryResponses);
        }

        public static async Task<IHttpActionResult> CreateMultipartActionAsync(this HttpRequestMessage request,
            IEnumerable<HttpResponseMessage> contents)
        {
            return (await request.CreateMultipartResponseAsync(contents)).ToActionResult();
        }

        public static async Task<HttpResponseMessage> CreateMultipartResponseAsync(this HttpRequestMessage request,
            IEnumerable<HttpResponseMessage> contents)
        {
            if (request.Headers.Accept.Contains(accept => accept.MediaType.ToLower().Contains("multipart/mixed")))
            {
                return request.CreateHttpMultipartResponse(contents);
            }

            return await request.CreateBrowserMultipartResponse(contents);
        }

        private static HttpResponseMessage CreateHttpMultipartResponse(this HttpRequestMessage request,
            IEnumerable<HttpResponseMessage> contents)
        {
            var multipartContent = new MultipartContent("mixed", "----Boundary_" + Guid.NewGuid().ToString("N"));
            request.CreateResponse(HttpStatusCode.OK, multipartContent);
            foreach (var content in contents)
            {
                multipartContent.Add(new HttpMessageContent(content));
            }
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = multipartContent;
            return response;
        }

        private static async Task<HttpResponseMessage> CreateBrowserMultipartResponse(this HttpRequestMessage request,
            IEnumerable<HttpResponseMessage> contents)
        {
            var multipartContentTasks = contents.NullToEmpty().Select(
                async (content) =>
                {
                    return await content.Content.HasValue(
                        async (contentContent) =>
                        {
                            var response = new Response
                            {
                                StatusCode = content.StatusCode,
                                ContentType = contentContent.Headers.ContentType,
                                ContentLocation = contentContent.Headers.ContentLocation,
                                Content = await contentContent.ReadAsStringAsync(),
                            };
                            return response;
                        },
                        () =>
                        {
                            var response = new Response
                            {
                                StatusCode = content.StatusCode,
                            };
                            return Task.FromResult(response);
                        });
                });

            var multipartContents = await Task.WhenAll(multipartContentTasks);
            var multipartResponseContent = new MultipartResponse
            {
                StatusCode = HttpStatusCode.OK,
                Content = multipartContents,
                Location = request.RequestUri,
            };

            var multipartResponse = request.CreateResponse(HttpStatusCode.OK, multipartResponseContent);
            multipartResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-multipart+json");
            return multipartResponse;
        }

        public static IHttpActionResult MergeIds<TResource>(this HttpRequestMessage request, Guid idUrl, TResource resource,
            Func<TResource, HttpActionDelegate> actionCallback,
            Func<Guid, WebId> createIdCallback)
            where TResource : ResourceBase
        {
            return resource.Id.GetUUID<IHttpActionResult>(
                (resourceId) => idUrl.HasValue<IHttpActionResult>(
                    (resourceIdUrl) =>
                    {
                        // Id's are specified in both places, ensure they match
                        if (resourceId != resourceIdUrl)
                            return request.CreateResponse(
                                    HttpStatusCode.BadRequest, "Incorrect URL for resource")
                                .ToActionResult();
                        var action = actionCallback(resource);
                        return action.ToActionResult();
                    },
                    () =>
                    {
                        // the URL id was not used, but the body has one,
                        // just do the call standard
                        HttpActionDelegate action = actionCallback(resource);
                        return action.ToActionResult();
                    }),
                () => idUrl.HasValue<IHttpActionResult>(
                    (resourceId) =>
                    {
                        // Only the URL has an id, 
                        // construct a resource with ID specified and return it.
                        var resourceWithId = resource.HasValue(
                            (value) => value,
                            () => Activator.CreateInstance<TResource>());
                        resourceWithId.Id = createIdCallback(resourceId);
                        HttpActionDelegate action = actionCallback(resource);
                        return action.ToActionResult();
                    },
                    () => request.CreateResponse(
                            HttpStatusCode.BadRequest, "No resource specified")
                        .ToActionResult()));
        }
        
    }
}
