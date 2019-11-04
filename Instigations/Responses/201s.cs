using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Reflection;
using EastFive.Api.Resources;


namespace EastFive.Api
{
    [StatusCodeResponse(StatusCode = HttpStatusCode.Created)]
    public delegate HttpResponseMessage CreatedResponse();
    
    [CreatedBodyResponse(StatusCode = HttpStatusCode.Created)]
    public delegate HttpResponseMessage CreatedBodyResponse<TResource>(TResource content, string contentType = default);
    public class CreatedBodyResponseAttribute : BodyTypeResponseAttribute
    {
        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var response = base.GetResponse(paramInfo, httpApp);
            response.Example = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
            return response;
        }
    }
}
