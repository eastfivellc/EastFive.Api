using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Api.Tests
{
    public delegate TResponse HttpActionDelegate<TResource, TResponse>(HttpResponseMessage response, TResource resource);
    public static class HttpActionHelpers
    {
        public static TResult GetContent<TResult>(this HttpResponseMessage response)
        {
            var content = response.Content as ObjectContent<TResult>;
            if (default(ObjectContent<TResult>) == content)
            {
                var expectedContentType = response.Content.GetType().GetGenericArguments().First();
                Assert.AreEqual(typeof(TResult).FullName, expectedContentType.FullName,
                    String.Format("Expected System.Net.Http.ObjectContent<{0}> but got type {1} in GET",
                        typeof(TResult).FullName, expectedContentType.FullName));
            }
            var results = (TResult)content.Value;
            return results;
        }
    }
}
