using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Controllers
{
    public struct Security
    {
        public Guid performingAsActorId;
        public System.Security.Claims.Claim[] claims;
    }

    public struct ContentBytes
    {
        public byte [] content;
        public MediaTypeHeaderValue contentType;
    }

    public struct ContentStream
    {
        public System.IO.Stream content;
        public MediaTypeHeaderValue contentType;
    }
}
