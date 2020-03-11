using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [ViewRenderer()]
    public delegate string ViewRenderer(string filePath, object content);
    public class ViewRendererAttribute : HtmlResponseAttribute
    {
        public override Task<HttpResponseMessage> InstigateInternal(IApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            ViewRenderer responseDelegate =
                (filePath, content) =>
                {
                    try
                    {
                        var parsedView = RazorEngine.Engine.Razor.RunCompile(filePath, null, content);
                        return parsedView;
                    }
                    catch (RazorEngine.Templating.TemplateCompilationException ex)
                    {
                        var body = ex.CompilerErrors.Select(error => error.ErrorText).Join(";\n\n");
                        return body;
                    }
                    catch (Exception ex)
                    {
                        return $"Could not load template {filePath} due to:[{ex.GetType().FullName}] `{ex.Message}`";
                    }
                };
            return onSuccess(responseDelegate);
        }
    }
}
