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
                HttpStatusCode.OK == response.StatusCode ||
                HttpStatusCode.NoContent == response.StatusCode);
        }

        public static async Task AssertSuccessPutAsync(this Task<HttpResponseMessage> responseTask)
        {
            var response = await responseTask;
            if(HttpStatusCode.Accepted != response.StatusCode &&
                HttpStatusCode.OK != response.StatusCode &&
                HttpStatusCode.NoContent != response.StatusCode)
            {
                var contentString = await response.Content.ReadAsStringAsync();
                var reason = contentString;
                try
                {
                    var resource = Newtonsoft.Json.JsonConvert.DeserializeObject<Exception>(contentString);
                    reason = resource.Message;
                }
                catch (Exception) { }
                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail("Status code: [{0}]\rReason:{1}", response.StatusCode, reason);
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

        public static HttpResponseMessage Assert(this HttpResponseMessage response, HttpStatusCode responseStatusCode)
        {
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(
                responseStatusCode, response.StatusCode, response.ReasonPhrase);
            return response;
        }

        public static async Task<HttpResponseMessage> AssertAsync(this Task<HttpResponseMessage> responseTask, HttpStatusCode responseStatusCode)
        {
            var response = await responseTask;
            if (response.StatusCode != responseStatusCode)
            {
                var reason = default(string);
                try
                {
                    var contentString = await response.Content.ReadAsStringAsync();
                    reason = contentString;
                    var resource = Newtonsoft.Json.JsonConvert.DeserializeObject<Exception>(contentString);
                    reason = resource.Message;
                }
                catch (Exception) { }
                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(
                    responseStatusCode, response.StatusCode, reason);
            }
            return await responseTask;
        }

        public static void AssertToMinute(this DateTime time1, DateTime time2)
        {
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(time1.DayOfYear, time2.DayOfYear);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(time1.Hour, time2.Hour);
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(time1.Minute, time2.Minute);
        }

        public static void AssertContains<T>(this IEnumerable<T> items, Func<T, bool> comparison)
        {
            foreach(var item in items)
            {
                if (comparison(item))
                    return;
            }
            Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail("Items does not contain item");
        }

        public static void AssertAll<T>(this IEnumerable<T> items, Func<T, bool> assertValid)
        {
            foreach (var item in items)
            {
                if (!assertValid(item))
                    Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail("Item {0} is not valid", item.ToString());
            }
        }
    }
}
