﻿using EastFive.Api.Meta.Postman.Resources.Collection;
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

        public string Scope { get; set; }

        public virtual Item GetItem(Method method)
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
                            exec = method.HasValue(
                                (v) => GetScriptSteps(v),
                                () => default(string[])),
                        },
                    }
                }
            };
        }

        private string [] GetScriptSteps(Method method)
        {
            if (method.IsDefaultOrNull())
                return new string[] { };

            var queryParamTestLines = new string[] { };
            var queryParamLineParams = method.MethodPoco
                .GetParametersAndAttributesInterface<IDefineWorkflowRequestVariable>()
                .ToArray();
            if (queryParamLineParams.Any())
            {
                var lineVarQuery = "var query = {};\r";
                var linePopulateQuery = "pm.request.url.query.all().forEach((param) => { query[param.key] = param.value});\r";
                var linesQueryVariables = queryParamLineParams
                    .SelectMany(
                        tpl =>
                        {
                            var (parameterInfo, attr) = tpl;
                            var (name, value) = attr.GetNameAndValue(parameterInfo, method);
                            var lineExtract = $"var queryParam_{name} = query[\"{value}\"];\r";
                            var lineMakeGlobal = $"pm.environment.set(\"{name}\", queryParam_{name});\r";
                            return new string[] { lineExtract, lineMakeGlobal, "\r" };
                        })
                    .ToArray();
                queryParamTestLines = linesQueryVariables
                    .Prepend(linePopulateQuery)
                    .Prepend(lineVarQuery)
                    .ToArray();
            }


            var headerParamTestLines = new string[] { };
            var headerLineParams = method.Responses
                .TryWhere((Resources.Response response, out IDefineWorkflowVariableFromHeader workflowResponse) =>
                    response.ParamInfo.TryGetAttributeInterface(out workflowResponse, inherit: true))
                .ToArray();
            if(headerLineParams.Any())
            {
                var lineVarHeaders = "var headers = {};\r";
                var linePopulateHeaders = "pm.response.headers.all().forEach((header) => { headers[header.key] = header.value });\r";
                var linesQueryHeaders = headerLineParams
                    .SelectMany(
                        itemAttrTpl =>
                        {
                            var (responseParameter, attr) = itemAttrTpl;
                            var (name, value) = attr.GetNameAndValue(responseParameter, method);
                            var lineExtract = $"var headerParam_{name} = headers[\"{value}\"];\r";
                            var lineMakeGlobal = $"pm.environment.set(\"{name}\", headerParam_{name});\r";
                            return new string[] { lineExtract, lineMakeGlobal, "\r" };
                        })
                    .ToArray();
                headerParamTestLines = linesQueryHeaders
                    .Prepend(linePopulateHeaders)
                    .Prepend(lineVarHeaders)
                    .ToArray();
            }

            var paramTypeTestLines = method.Responses
                .TryWhere((Resources.Response response, out IDefineWorkflowResponse workflowResponse) =>
                    response.ParamInfo.ParameterType.TryGetAttributeInterface(out workflowResponse, inherit: true))
                .SelectMany(tpl => tpl.@out.GetPostmanTestLines(tpl.item, method))
                .ToArray();

            return queryParamTestLines
                .Concat(headerParamTestLines)
                .Concat(paramTypeTestLines).ToArray();
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

        protected virtual Request GetRequest(Method method)
        {
            var bodyProperties = method.MethodPoco
                .GetParameters()
                .TryWhere((ParameterInfo paramInfo, out IDefineWorkflowRequestProperty requestProperty) =>
                    paramInfo.TryGetAttributeInterface(out requestProperty))
                .ToArray();

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
                    raw = $"{{{{HOST}}}}/{method.Route.Namespace}/{method.Route.Name}",
                    host = Url.VariableHostName.AsArray(),
                    path = new string[] { method.Route.Namespace, method.Route.Name },
                    query = paramQueryItems.Concat(attrQueryItems).ToArray(),
                },
                description = GetDescription(),
            };


            Body PopulateBody()
            {

                //if (IsFormDataCapable())
                //    return PopulateJsonBody();

                var formdata = FormDataBody();
                var rawJsonBody = RawJsonBody();

                var mode = IsFormDataCapable() ?
                    "formdata"
                    :
                    "raw";

                return new Body()
                {
                    mode = mode,
                    options = new Options()
                    {
                        raw = new Raw()
                        {
                            language = "json",
                        }
                    },
                    formdata = formdata,
                    raw = rawJsonBody,
                };

                bool IsFormDataCapable() => formdata.Length == bodyProperties.Length;

                FormData[] FormDataBody()
                {
                    return bodyProperties
                        .Where(
                            tpl => typeof(IDefineWorkflowRequestPropertyFormData)
                                .IsAssignableFrom(tpl.@out.GetType()))
                        .Select(tpl => ((IDefineWorkflowRequestPropertyFormData)tpl.@out, tpl.item))
                        .SelectMany(attr => attr.Item1.GetFormData(attr.item))
                        .ToArray();
                }

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

                bool IsBodyEmpty()
                {
                    if (!formdata.AnyNullSafe())
                        return false;

                    if (rawJsonBody.Trim(new char[] {'{','}'}).HasBlackSpace())
                        return false;

                    return true;
                }
            } 

            Body PopulateJsonBody()
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

    public class WorkflowStepCustomAttribute : WorkflowStepAttribute
    {
        public string HttpMethod { get; set; }

        public string[] HeaderKeys { get; set; }

        public string[] HeaderValues { get; set; }

        public string BodyRaw { get; set; }

        public bool IsBodyFile { get; set; }

        public string Url { get; set; }

        public string Description { get; set; }

        protected override Request GetRequest(Method method)
        {
            bool didParseUrl = Uri.TryCreate(this.Url, UriKind.RelativeOrAbsolute, out Uri parsedUri);
            return new Request()
            {
                method = this.HttpMethod,
                header = this.HeaderKeys
                    .CollateSimple(this.HeaderValues)
                    .Select(tpl => new Header() { key = tpl.Item1, value = tpl.Item2  })
                    .ToArray(),
                body = this.BodyRaw.HasBlackSpace()?
                    new Body
                    {
                        mode = "raw",
                        raw = this.BodyRaw,
                    }
                    :
                    this.IsBodyFile?
                        new Body {
                            mode = "file",
                            file = new object() { },
                        }
                        :
                        default(Body),
                url = //didParseUrl?
                    //parsedUri.IsAbsoluteUri?
                    //    new Url()
                    //    {
                    //        raw = $"{{{{HOST}}}}/{this.Url}",
                    //        host = "{{HOST}}".AsArray(),
                    //        path = parsedUri.PathAndQuery
                    //            .Split('/')
                    //            .Where(item => !item.Contains('?'))
                    //            .ToArray(),
                    //        query = parsedUri
                    //            .ParseQuery()
                    //            .Select(kvp => new QueryItem() { key = kvp.Key, value = kvp.Value })
                    //            .ToArray(),
                    //    }
                    //    :
                    //    new Url()
                    //    {
                    //        raw = this.Url,
                    //        host = this.Url.AsArray(),
                    //    }
                    //:
                        new Url()
                        {
                            raw = this.Url,
                            host = this.Url.AsArray(),
                        },
                description = this.Description,
            };
        }
    }

    public class WorkflowStepCustom2Attribute : WorkflowStepCustomAttribute
    {

    }
}
