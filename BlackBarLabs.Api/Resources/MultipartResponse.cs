using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;

namespace BlackBarLabs.Api.Resources
{
    [DataContract]
    public class MultipartResponse : BlackBarLabs.Api.Resource
    {
        public MultipartResponse()
        {
            Content = new List<Response>();
        }

        [DataMember(Name = "location")]
        public Uri Location { get; set; }

        [DataMember(Name = "statusCode")]
        public HttpStatusCode StatusCode { get; set; }

        [DataMember(Name = "content")]
        public List<Response> Content { get; set; }
    }
}
