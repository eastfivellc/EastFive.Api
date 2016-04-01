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
    public static class AssertExtensions
    {
        public static void AssertSuccessPut(this HttpResponseMessage response)
        {
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(
                HttpStatusCode.Accepted == response.StatusCode ||
                HttpStatusCode.OK == response.StatusCode);
        }

        public static async Task AssertSuccessPutAsync(this Task<HttpResponseMessage> responseTask)
        {
            var response = await responseTask;
            if(HttpStatusCode.Accepted == response.StatusCode ||
                HttpStatusCode.OK == response.StatusCode)
            {
                var contentString = await response.Content.ReadAsStringAsync();
                var reason = contentString;
                try
                {
                    var resource = Newtonsoft.Json.JsonConvert.DeserializeObject<Exception>(contentString);
                    reason = resource.Message;
                }
                catch (Exception) { }
                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail(reason, new { response.StatusCode });
            }
        }

        public static void AssertSuccessDelete(this HttpResponseMessage response)
        {
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(
                HttpStatusCode.Accepted == response.StatusCode ||
                HttpStatusCode.OK == response.StatusCode);
        }

        public static async Task AssertSuccessDeleteAsync(this Task<HttpResponseMessage> responseTask)
        {
            var response = await responseTask;
            response.AssertSuccessDelete();
        }

        public static void Assert(this HttpResponseMessage response, HttpStatusCode responseStatusCode)
        {
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(
                responseStatusCode, response.StatusCode, response.ReasonPhrase);
        }

        public static async Task AssertAsync(this Task<HttpResponseMessage> responseTask, HttpStatusCode responseStatusCode)
        {
            var response = await responseTask;
            if (response.StatusCode != responseStatusCode)
            {
                var contentString = await response.Content.ReadAsStringAsync();
                var reason = contentString;
                try
                {
                    var resource = Newtonsoft.Json.JsonConvert.DeserializeObject<Exception>(contentString);
                    reason = resource.Message;
                }
                catch (Exception) { }
                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(
                    responseStatusCode, response.StatusCode, reason);
            }
        }
    }
}
