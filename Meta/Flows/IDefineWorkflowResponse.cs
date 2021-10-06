using EastFive.Api.Resources;
using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineWorkflowResponse
    {
        string[] GetPostmanTestLines(Resources.Response item, Method method);
    }

    public class WorkflowResponseDefinition : System.Attribute, IDefineWorkflowResponse
    {
        public string[] GetPostmanTestLines(Resources.Response response, Method method)
        {
            var resourceType = method.Route.Type;
            var ifCheckStart = $"if(pm.response.headers.members.some(function(element) {{ return element.key == \"{Core.Middleware.HeaderStatusName}\" && element.value == \"{response.ParamInfo.Name}\" }})) {{";
            var ifCheckEnd = "}\r";
            if (response.IsMultipart)
            {
                var parseLine = "\tlet resourceList = pm.response.json();\r";
                var loopBegin = "\tfor(const resourceIndex in resourceList) {\r";
                var varResource = "\t\tlet resource = resourceList[resourceIndex];\r";

                var resourceAssignments = GetResourceAssignments(out string discard1, out string discard2);

                var loopEnd = "\t}\r";

                return new string[][]
                {
                    new string []
                    {
                        ifCheckStart,
                        parseLine,
                        loopBegin,
                        varResource,
                    },
                    resourceAssignments,
                    new string []
                    {
                        loopEnd,
                        ifCheckEnd,
                    },
                }.SelectMany().ToArray();
            }

            {
                var parseLine = "\tlet resource = pm.response.json();\r";
                var resourceAssignments = GetResourceAssignments(out string resourceTypeName, out string resourceNameName);
                var lastResourceAssignment = resourceNameName.HasBlackSpace() ?
                    $"\tpm.environment.set(\"{resourceTypeName}\", resourceId);\r"
                    :
                    string.Empty;

                var finalProperties = response.ParamInfo
                    .GetAttributesInterface<IDefineWorkflowVariable>()
                    .Select(extraVariableDefinition => extraVariableDefinition.GetNameAndValue(response, method))
                    .Select(
                        tpl => $"\tpm.environment.set(\"{tpl.Item1}\", resource.{tpl.Item2});\r")
                    .ToArray();

                return new string[][]
                {
                    new string []
                    {
                        ifCheckStart,
                        parseLine,
                    },
                    resourceAssignments,
                    finalProperties,
                    new string []
                    {
                        lastResourceAssignment,
                        ifCheckEnd,
                    },
                }.SelectMany().ToArray();
            }

            string[] GetResourceAssignments(out string resourceTypeName,
                out string resourceNameName)
            {
                var resourceName = method.Route.Name;
                resourceTypeName = $"{resourceName}";

                var idProperties = method.Route.Properties.Where(prop => prop.IsIdentfier).ToArray();
                if (!idProperties.Any())
                {
                    resourceNameName = string.Empty;
                    return new string[] { };
                }
                var idProperty = idProperties.First().Name;

                resourceNameName = resourceType
                    .GetPropertyAndFieldsWithAttributesInterface<ITitleResource>()
                    .First(
                        (tpl, next) =>
                        {
                            return tpl.Item1.TryGetAttributeInterface(out IProvideApiValue apiValueProvider) ?
                                apiValueProvider.PropertyName
                                :
                                tpl.Item1.Name;
                        },
                        () => idProperty);

                var varResourceId = $"\t\tlet resourceId = resource.{idProperty};\r";

                var varResourceName = $"\t\tlet resourceName = resource.{resourceNameName};\r";
                var varNameVariable = "\t\tlet resourceNameVariable = resourceName.replace(/[^A-Z0-9]/ig, \"_\");\r";

                var varNameVariableName = $"\t\tlet resourceNameVariableName = \"{resourceTypeName}_\" + resourceNameVariable;\r";
                var setEnvVariable = "\t\tpm.environment.set(resourceNameVariableName, resourceId);\r";

                return new string[]
                {
                    varResourceId, varResourceName, varNameVariable, varNameVariableName, setEnvVariable
                };
            }

        }
    }
}
