using BlackBarLabs.Web;
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
        
        public static Guid GetUserId(this ITestSession session)
        {
            var authorizationHeader = session.Headers["Authorization"];
            var claims = authorizationHeader.GetClaimsJwtString();
            return claims.GetAccountId(
                (accountId) => accountId,
                () => Guid.Empty);
        }

    }
}
