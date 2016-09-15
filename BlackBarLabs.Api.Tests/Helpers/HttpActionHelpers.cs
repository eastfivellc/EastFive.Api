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

        public static TModel GetContent<TModel>(this HttpResponseMessage response)
        {
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
