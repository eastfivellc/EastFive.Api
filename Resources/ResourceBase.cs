using System;
using System.Runtime.Serialization;

using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace BlackBarLabs.Api
{
    [DataContract]
    public class ResourceBase
    {
        public const string IdPropertyName = "id";
        [JsonProperty(PropertyName = IdPropertyName)]
        [DataMember(Name = IdPropertyName)]
        public WebId Id { get; set; }
    }
}
