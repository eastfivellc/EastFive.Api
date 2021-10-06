using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using EastFive.Extensions;
using EastFive.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineFlow
    {
        public string FlowName { get; }

        public double Step { get; }

        Item GetItem(Api.Resources.Method method);
    }

    public class WorkflowStepAttribute : System.Attribute, IDefineFlow
    {
        public string FlowName { get; set; }

        public string StepName { get; set; }

        public double Step { get; set; }

        public string Scope { get; }

        public Item GetItem(Method method)
        {
            return new Item
            {
                name = GetStepName(method),
                request = GetRequest(method),
                _event = new Event []
                {
                    new Event()
                    {
                        listen = "test",
                        script = new Script()
                        {
                            type = "text/javascript",
                            exec = GetScriptSteps(method),
                        },
                    }
                }
            };
        }

        private string [] GetScriptSteps(Method method)
        {
            return method.Responses
                .TryWhere((Resources.Response response, out IDefineWorkflowResponse workflowResponse) =>
                    response.ParamInfo.ParameterType.TryGetAttributeInterface(out workflowResponse))
                .SelectMany(tpl => tpl.@out.GetPostmanTestLines(tpl.item, method))
                .ToArray();
        }

        private string GetStepName(Method method)
        {
            if (StepName.HasBlackSpace())
                return StepName;

            if (method.HttpMethod.Equals(HttpMethod.Post.Method, System.StringComparison.OrdinalIgnoreCase))
                return $"Create {method.Route.Name}";

            if (method.HttpMethod.Equals(HttpMethod.Put.Method, System.StringComparison.OrdinalIgnoreCase))
                return $"Replace {method.Route.Name}";

            if (method.HttpMethod.Equals(HttpMethod.Delete.Method, StringComparison.OrdinalIgnoreCase))
                return $"Delete {method.Route.Name}";

            if (method.HttpMethod.Equals(HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase))
            {
                if(method.Responses.Any(response => response.IsMultipart))
                    return $"List {method.Route.Name}";
                return $"Get {method.Route.Name}";
            }

            if (method.HttpMethod.Equals(HttpMethod.Patch.Method, StringComparison.OrdinalIgnoreCase))
                return $"Update {method.Route.Name}";
            
            return method.Route.Name;
        }

        private Request GetRequest(Method method)
        {
            var bodyProperties = method.MethodPoco
                .GetParameters()
                .TryWhere((ParameterInfo paramInfo, out IDefineWorkflowRequestProperty requestProperty) =>
                    paramInfo.TryGetAttributeInterface(out requestProperty));

            var body = bodyProperties.IsDefaultNullOrEmpty() ?
                default(Body)
                :
                PopulateBody();

            var paramQueryItems = method.MethodPoco
                        .GetParameters()
                        .TryWhere((ParameterInfo paramInfo, out IDefineQueryItem requestProperty) =>
                            paramInfo.TryGetAttributeInterface(out requestProperty))
                        .Select(tpl => tpl.@out.GetQueryItem(method, tpl.item))
                        .SelectWhereHasValue();
            var attrQueryItems = method
                .MethodPoco
                .CustomAttributes
                .SelectMany(
                    attr =>
                    {
                        return attr.AttributeType.GetAttributesInterface<IDefineQueryItem>();
                    })
                .SelectMany(attr => attr.GetQueryItems(method));

            return new Request()
            {
                method = method.HttpMethod,
                header = method.MethodPoco
                    .GetParameters()
                    .TryWhere(
                        (ParameterInfo paramInfo, out IDefineHeader headerDefinition) =>
                        {
                            if (paramInfo.TryGetAttributeInterface(out headerDefinition))
                                return true;

                            // See if the parameter type has a header requirement
                            if (paramInfo.ParameterType.TryGetAttributeInterface(out headerDefinition))
                                return true;
                            return false;
                        })
                    .Select(tpl => tpl.@out.GetHeader(method, tpl.item))
                    .ToArray(),
                body = body,
                url = new Url()
                {
                    raw = $"{{{{HOST}}}}/api/{method.Route.Name}",
                    host = "{{HOST}}".AsArray(),
                    path = new string[] { "api", method.Route.Name },
                    query = paramQueryItems.Concat(attrQueryItems).ToArray(),
                },
                description = GetDescription(),
            };


            Body PopulateBody()
            {
                return new Body()
                {
                    mode = "raw",
                    options = new Options()
                    {
                        raw = new Raw()
                        {
                            language = "json",
                        }
                    },
                    raw = RawJsonBody(),
                };

                string RawJsonBody()
                {
                    var sb = new StringBuilder();
                    using (var sw = new StringWriter(sb))
                    {
                        var jsonSerializer = new JsonSerializer();
                        using (var writer = new JsonTextWriter(sw)
                        {
                            Formatting = Formatting.Indented,
                        })
                        {
                            writer.WriteStartObject();
                            foreach (var requestProperty in bodyProperties)
                            {
                                requestProperty.@out.AddProperties(writer, requestProperty.item);
                            }
                            writer.WriteEndObject();
                        }
                        return sb.ToString();
                    }
                }
            }

            string GetDescription()
            {
                return method.Description;
            }
        }
    }

    public class WorkflowStep2Attribute : WorkflowStepAttribute
    {

    }

    public class WorkflowStep3Attribute : WorkflowStepAttribute
    {

    }
}
