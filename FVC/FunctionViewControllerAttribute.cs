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
        public string Namespace { get; set; }
        public string Route { get; set; }
        public Type Resource { get; set; }
        public string ContentType { get; set; }
        public string ContentTypeVersion { get; set; }
        public string [] ContentTypeEncodings { get; set; }

        public Uri GetRelativePath(Type resourceDecorated)
        {
            var routeDirectory = this.Namespace.HasBlackSpace() ?
                this.Namespace
                :
                Web.Configuration.Settings.GetString(
                        AppSettings.DefaultNamespace,
                    (ns) => ns,
                    (whyUnspecifiedOrInvalid) => "api");

            var route = this.Route.HasBlackSpace() ?
                this.Route
                :
                resourceDecorated.Name;

            return new Uri($"{routeDirectory}/{route}", UriKind.Relative);
        }

    }
}
