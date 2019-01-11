using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class FunctionViewControllerAttribute : Attribute
    {
        public string Route { get; set; }
        public Type Resource { get; set; }
        public string ContentType { get; set; }
        public string ContentTypeVersion { get; set; }
        public string [] ContentTypeEncodings { get; set; }

    }
}
