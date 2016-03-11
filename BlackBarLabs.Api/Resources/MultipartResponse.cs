using System;
using System.Collections.Generic;
using System.Net;

namespace BlackBarLabs.Api.Resources
{
    public class MultipartResponse
    {
        public MultipartResponse()
        {
            Content = new List<Response>();
        }

        public Uri Location { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public List<Response> Content { get; set; }
    }
}
