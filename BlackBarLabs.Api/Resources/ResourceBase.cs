using System;
using System.Runtime.Serialization;

using BlackBarLabs.Api.Resources;

namespace BlackBarLabs.Api
{
    public class ResourceBase
    {
        [DataMember(Name = "id")]
        public WebId Id { get; set; }
    }
}
