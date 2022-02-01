using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Serialization;
using EastFive.Api.Resources;

namespace EastFive.Api.Meta.Flows
{
    public class WorkflowVariableArrayPropertyArrayAttribute : System.Attribute, IDefineWorkflowScriptResponse
    {
        public string PropertyName { get; set; }

        public string VariableName { get; set; }

        public string[] GetInitializationLines(Response response, Method method)
        {
            return new string[] { };
        }

        public string[] GetScriptLines(Response response, Method method)
        {
            var resourceType = response.ParamInfo.ParameterType.TryGetAttributeInterface(out IProvideResponseType responseTypeProvider) ?
                responseTypeProvider.GetResponseType(response.ParamInfo)
                :
                method.Route.Type;
            var ifCheckStart = $"if(pm.response.headers.members.some(function(element) {{ return element.key == \"{Core.Middleware.HeaderStatusName}\" && element.value == \"{response.ParamInfo.Name}\" }})) {{";
            var ifCheckEnd = "}\r";

            var parseLine = "\tlet resourceList = pm.response.json();\r";
            var mapLine = $"\tlet propertyArray = objArray.map(a => a.{this.PropertyName});\r";
            var setEnvVariable = $"\tpm.environment.set({this.VariableName}, propertyArray);\r";

            return new string[]
            {
                ifCheckStart,
                parseLine,
                mapLine,
                setEnvVariable,
                ifCheckEnd,
            };
        }
    }
}
