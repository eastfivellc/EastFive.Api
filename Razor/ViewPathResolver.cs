using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [ViewPath]
    public delegate string ViewPathResolver(string view);
    public class ViewPathAttribute : HtmlResponseAttribute
    {
        public override Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ViewPathResolver responseDelegate =
                (viewPath) =>
                {
                    return $"{System.Web.HttpRuntime.AppDomainAppPath}Views\\{viewPath}";
                };
            return onSuccess(responseDelegate);
        }
    }
}
