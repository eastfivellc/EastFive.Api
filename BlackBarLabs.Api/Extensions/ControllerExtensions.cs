using BlackBarLabs.Api.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Core.Extensions;

namespace BlackBarLabs.Api
{
    public static class ControllerExtensions
    {
        public static async Task<TResult> ParseMultipartAsync<TResult, TMethod>(this HttpContent content,
            Expression<TMethod> callback)
        {
            if (!content.IsMimeMultipartContent())
            {
                throw new ArgumentException("Content is not multipart", "content");
            }

            var streamProvider = new MultipartMemoryStreamProvider();
            await content.ReadAsMultipartAsync(streamProvider);

            var paramTasks = callback.Parameters.Select(
                async (param) =>
                {
                    var paramContent = streamProvider.Contents.FirstOrDefault(file => file.Headers.ContentDisposition.Name.Contains(param.Name));
                    if (default(HttpContent) == paramContent)
                        return param.Type.IsValueType ? Activator.CreateInstance(param.Type) : null;

                    if (param.Type.GUID == typeof(string).GUID)
                    {
                        var stringValue = await paramContent.ReadAsStringAsync();
                        return (object)stringValue;
                    }
                    if (param.Type.GUID == typeof(Guid).GUID)
                    {
                        var guidStringValue = await paramContent.ReadAsStringAsync();
                        var guidValue = Guid.Parse(guidStringValue);
                        return (object)guidValue;
                    }
                    if (param.Type.GUID == typeof(System.IO.Stream).GUID)
                    {
                        var streamValue = await paramContent.ReadAsStreamAsync();
                        return (object)streamValue;
                    }
                    if (param.Type.GUID == typeof(byte []).GUID)
                    {
                        var byteArrayValue = await paramContent.ReadAsByteArrayAsync();
                        return (object)byteArrayValue;
                    }
                    var value = await paramContent.ReadAsAsync(param.Type);
                    return value;
                });

            var paramsForCallback = await Task.WhenAll(paramTasks);
            var result = ((LambdaExpression)callback).Compile().DynamicInvoke(paramsForCallback);
            return (TResult)result;
        }

        public static IHttpActionResult ToActionResult(this HttpActionDelegate action)
        {
            return new HttpActionResult(action);
        }
        public static IHttpActionResult ToActionResult(this HttpResponseMessage response)
        {
            return new HttpActionResult(() => Task.FromResult(response));
        }

        public static IHttpActionResult ActionResult(this ApiController controller, HttpActionDelegate action)
        {
            return action.ToActionResult();
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

        public static async Task<IHttpActionResult> CreateMultipartActionAsync(this HttpRequestMessage request,
            IEnumerable<HttpResponseMessage> contents)
        {
            return (await request.CreateMultipartResponseAsync(contents)).ToActionResult();
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
            var contentTasks = contents.Select(
                async (content) =>
                {
                    var response = new Response
                    {
                        StatusCode = content.StatusCode,
                        ContentType = content.Content.Headers.ContentType,
                        ContentLocation = content.Content.Headers.ContentLocation,
                        Content = await content.Content.ReadAsStringAsync(),
                    };
                    return response;
                });

            var multipartResponseContent = new MultipartResponse
            {
                StatusCode = HttpStatusCode.OK,
                Content = (await Task.WhenAll(contentTasks)).ToList(),
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
