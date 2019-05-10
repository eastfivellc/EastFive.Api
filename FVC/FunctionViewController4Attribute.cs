using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class FunctionViewController4Attribute : FunctionViewControllerAttribute, IProvideSerialization
    {
        public string MediaType => "application/json";

        public HttpResponseMessage Serialize(HttpResponseMessage response, 
            HttpApplication httpApp, HttpRequestMessage request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeConvert(httpApp, request);
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj, new JsonConverter[] { converter } );
            var contentType = this.ContentType.HasBlackSpace() ?
                this.ContentType
                :
                this.MediaType;
            response.Content = new StringContent(jsonObj, Encoding.UTF8, contentType);
            return response;
        }
    }
}
