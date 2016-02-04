using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Api.Tests
{
    public static class TestSessionHelpers
    {
        public static TestSession GetTestSession(this System.Security.Principal.IPrincipal user)
        {
            if (!(user is TestUser))
                Assert.Fail("This class is only for testing, not real situation");
            var mockPrincipal = user as TestUser;
            return mockPrincipal.Session;
        }

        public static Guid GetId(this System.Security.Principal.IPrincipal user)
        {
            if (!(user is TestUser))
                Assert.Fail("This class is only for testing, not real situation");
            var mockPrincipal = user as TestUser;
            return mockPrincipal.Id;
        }

        #region Multipart Content

        public static void AddContent(this MultipartContent multipart, string name, Guid content)
        {
            var streamContent = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(content.ToString())));
            streamContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("string");
            streamContent.Headers.ContentDisposition.Name = String.Format("\"{0}\"", name);
            multipart.Add(streamContent);
        }

        public static void AddContent(this MultipartContent multipart, string name, Stream content)
        {
            var streamContent = new StreamContent(content);
            streamContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("file");
            streamContent.Headers.ContentDisposition.Name = String.Format("\"{0}\"", name);
            multipart.Add(streamContent);
        }

        #endregion

        #region Invocation

        #endregion
    }
}
