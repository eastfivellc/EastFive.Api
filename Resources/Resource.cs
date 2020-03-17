using System;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc.Routing;

using BlackBarLabs.Web;
using EastFive.Api.Services;
using System.Linq;

namespace BlackBarLabs.Api
{
    [Obsolete("Use ToActionResult instead")]
    public class Resource
    {
        public void Configure(HttpRequestMessage request, UrlHelper url)
        {
            this.Request = request;
            this.Url = url;
        }

        [IgnoreDataMember]
        public HttpRequestMessage Request { get; set; }

        [IgnoreDataMember]
        protected UrlHelper Url { get; private set; }

    }
}
