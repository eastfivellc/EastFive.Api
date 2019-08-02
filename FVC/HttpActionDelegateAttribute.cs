using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Api.Resources;

namespace EastFive.Api
{
    public class HttpActionDelegateAttribute : Attribute, IDocumentResponse
    {
        public HttpStatusCode StatusCode { get; set; }

        public Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Response()
            {
                Name = paramInfo.Name,
                StatusCode = this.StatusCode,
                Example = "",
                Headers = new KeyValuePair<string, string>[] { },
            };
        }
    }
}
