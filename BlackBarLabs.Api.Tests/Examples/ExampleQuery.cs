using BlackBarLabs.Api.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Api.Tests.Examples
{
    public class ExampleQuery : ResourceQueryBase
    {
        public WebIdQuery AssociatedId { get; set; }

        int Value { get; set; }
    }
}
