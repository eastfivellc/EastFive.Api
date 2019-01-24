using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class HttpActionDelegateAttribute : Attribute
    {
        public HttpStatusCode StatusCode { get; set; }
    }
}
