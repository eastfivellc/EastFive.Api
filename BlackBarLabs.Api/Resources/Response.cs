using System;
using System.Net;
using System.Net.Mime;

namespace BlackBarLabs.Api.Resources
{
    public class Response
    {
        public string ETag { get; set; }

        public Uri Location { get; set; }

        public Uri ContentLocation { get; set; }

        public DateTime LastModified { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public ContentType ContentType { get; set; }

        public object Content { get; set; }
    }
}
