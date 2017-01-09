using System;
using System.Runtime.Serialization;

using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace BlackBarLabs.Api
{
    [DataContract]
    public class ResourceQueryBase
    {
        [JsonProperty(PropertyName = "id")]
        public WebIdQuery Id { get; set; }
    }
}
