using BlackBarLabs.Api.Tests.Examples;
using BlackBarLabs.Core.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Routing;

namespace BlackBarLabs.Api.Tests.Tests
{
    [TestClass]
    public class QueryParsingTests
    {
        [TestMethod]
        public async Task ParseQueryForId()
        {
            var queryId = Guid.NewGuid();
            var query = new ExampleQuery() { Id = queryId };

            bool ran = false;
            Func<Guid, Task<HttpResponseMessage>> assert =
                (id) =>
                {
                    Assert.AreEqual(queryId, id);
                    ran = true;
                    return default(HttpResponseMessage).ToTask();
                };

            var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/foo/" + queryId.ToString());
            request.SetConfiguration(new System.Web.Http.HttpConfiguration());
            await query.ParseAsync(request,
                (q) => assert(q.Id.ParamSingle()));

            Assert.IsTrue(ran);
        }

        [TestMethod]
        public async Task ParseQueryForAssociatedId()
        {
            var queryId = Guid.NewGuid();
            var query = new ExampleQuery() { AssociatedId = queryId };

            bool ran = false;
            Func<Guid, Task<HttpResponseMessage>> assert =
                (id) =>
                {
                    Assert.AreEqual(queryId, id);
                    ran = true;
                    return default(HttpResponseMessage).ToTask();
                };

            var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/foo?associated_id=" + queryId.ToString());
            request.SetConfiguration(new System.Web.Http.HttpConfiguration());
            await query.ParseAsync(request,
                (q) => assert(q.Id.ParamSingle()),
                (q) => assert(q.AssociatedId.ParamSingle()));

            Assert.IsTrue(ran);
        }

        [TestMethod]
        public async Task ParseQueryForArray()
        {
            var queryIds = Enumerable.Range(0, 10).Select(i => Guid.NewGuid()).ToArray();
            var query = new ExampleQuery() { Id = queryIds };

            bool ran = false;
            Func<Guid, Task<HttpResponseMessage>> assert =
                (id) =>
                {
                    Assert.Fail();
                    return default(HttpResponseMessage).ToTask();
                };
            Func<Guid[], Task<HttpResponseMessage[]>> assertMulti =
                (ids) =>
                {
                    Assert.AreEqual(queryIds.Length, ids.Length);
                    ran = true;
                    return default(HttpResponseMessage[]).ToTask();
                };

            var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/foo/" + queryIds.Select(id => id.ToString())
                .Join(","));
            request.SetConfiguration(new System.Web.Http.HttpConfiguration());
            await query.ParseAsync(request,
                (q) => assert(q.Id.ParamSingle()),
                (q) => assert(q.AssociatedId.ParamSingle()),
                (q) => assertMulti(q.Id.ParamOr()));

            Assert.IsTrue(ran);
        }
    }
}
