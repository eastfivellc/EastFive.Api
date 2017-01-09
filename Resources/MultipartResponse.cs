using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;

namespace BlackBarLabs.Api.Resources
{
    [DataContract]
    public class MultipartResponse
    {
        public MultipartResponse()
        {
            Content = new Response [] { };
        }

        [DataMember(Name = "location")]
        public Uri Location { get; set; }

        [DataMember(Name = "statusCode")]
        public HttpStatusCode StatusCode { get; set; }

        [DataMember(Name = "content")]
        public Response [] Content { get; set; }
    }
}
