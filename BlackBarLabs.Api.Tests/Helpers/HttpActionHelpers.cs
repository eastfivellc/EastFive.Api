using BlackBarLabs.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Api.Tests
{
    public delegate TResponse HttpActionDelegate<TResource, TResponse>(HttpResponseMessage response, TResource resource);
    public static class HttpActionHelpers
    {
        public static IEnumerable<TModel> GetContentMultipart<TModel>(this HttpResponseMessage response)
            where TModel : class
        {
            var contentIEnumerable = response.Content as ObjectContent<IEnumerable<TModel>>;
            if (default(ObjectContent<IEnumerable<TModel>>) != contentIEnumerable)
                return (IEnumerable<TModel>)contentIEnumerable.Value;

            var contentArray = response.Content as ObjectContent<TModel[]>;
            if (default(ObjectContent<TModel[]>) != contentArray)
            {
                var arrayValue = (TModel[])contentArray.Value;
                return arrayValue;
            }

            var contentMultipart = response.Content as ObjectContent<Resources.MultipartResponse>;
            if (default(ObjectContent<Resources.MultipartResponse>) != contentMultipart)
            {
                var multipartValue = (Resources.MultipartResponse)contentMultipart.Value;
                if (typeof(Resources.Response) == typeof(TModel))
                    return multipartValue.Content.Select(content => content as TModel);
                
                var multipartContent = multipartValue.Content.Select(
                    (resource) =>
                    {
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<TModel>(resource.Content as string);
                    });
                return multipartContent;
            }

            var singleContent = response.GetContent<TModel>();
            return singleContent.ToEnumerable();
        }

        public static async Task<IEnumerable<TModel>> GetContentMultipartAsync<TModel>(this Task<HttpResponseMessage> responseRetrieving)
            where TModel : class
        {
            var response = await responseRetrieving;
            return response.GetContentMultipart<TModel>();
        }

        public static TModel GetContent<TModel>(this HttpResponseMessage response)
        {
            if (null == response.Content)
                Assert.Fail($"Response was empty: {response}");
            var content = response.Content as ObjectContent<TModel>;
            if (default(ObjectContent<TModel>) == content)
            {
                // TODO: Check base types
                var expectedContentType = response.Content.GetType().GetGenericArguments().First();
                Assert.AreEqual(typeof(TModel).FullName, expectedContentType.FullName,
                    String.Format("Expected {0} but got type {1} in GET",
                        typeof(TModel).FullName, expectedContentType.FullName));
            }
            var results = (TModel)content.Value;
            return results;
        }

        public static async Task<TModel> GetContentAsync<TModel>(this Task<HttpResponseMessage> responseTask)
        {
            var response = await responseTask;
            return response.GetContent<TModel>();
        }
        public static async Task<TModel> GetContentAsync<TModel>(this Task<HttpResponseMessage> responseTask, HttpStatusCode assertStatusCode)
        {
            var response = await responseTask;
            response.Assert(assertStatusCode);
            return response.GetContent<TModel>();
        }
    }
}
