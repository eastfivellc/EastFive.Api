using System;
using System.Runtime.Serialization;

using BlackBarLabs.Api.Resources;

namespace BlackBarLabs.Api
{
    [DataContract]
    public class ResourceQueryBase
    {
        [DataMember(Name = "id")]
        public WebIdQuery Id { get; set; }
    }
}
