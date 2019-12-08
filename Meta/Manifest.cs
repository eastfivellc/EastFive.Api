using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Resources
{
    public class Manifest
    {
        public Manifest()
        {

        }

        public Manifest(IEnumerable<Type> lookups,
            HttpApplication httpApp)
        {
            this.Routes = lookups
                .Where(type => type.ContainsAttributeInterface<IDocumentRoute>())
                .Select(type => type.GetAttributesInterface<IDocumentRoute>().First().GetRoute(type, httpApp))
                .OrderBy(route => route.Name)
                .ToArray();
        }

        public Route[] Routes { get; set; }
    }

    public class Response
    {
        public Response(ParameterInfo paramInfo)
        {
            this.Name = paramInfo.Name;
            this.StatusCode = System.Net.HttpStatusCode.OK;
            //this.Example = "TODO: JSON serialize response type";
            this.Headers = new KeyValuePair<string, string>[] { };
        }

        public Response()
        {
        }

        public string Name { get; set; }

        public System.Net.HttpStatusCode StatusCode { get; set; }

        public string Example { get; set; }

        public KeyValuePair<string, string>[] Headers { get; set; }
    }
}
