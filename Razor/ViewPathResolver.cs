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
        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ViewPathResolver responseDelegate =
                (viewPath) =>
                {
                    return $"{Razor.RazorTemplateManager.systemPath}Views\\{viewPath}";
                };
            return onSuccess(responseDelegate);
        }
    }
}
