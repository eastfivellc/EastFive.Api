using System;
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
using System.Reflection;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineWorkflowRequestVariable
    {
        (string, string) GetNameAndValue(ParameterInfo response, Method method);
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class WorkflowVariableRequestAttribute
        : Attribute, IDefineWorkflowRequestVariable
    {
        public string VariableName { get; set; }

        public string PropertyName { get; set; }

        public WorkflowVariableRequestAttribute(string variableName, string propertyName)
        {
            this.VariableName = variableName;
            this.PropertyName = propertyName;
        }

        public (string, string) GetNameAndValue(ParameterInfo response, Method method)
        {
            //method.Route.Properties
            //    .Where(prop => prop.Name == this.PropertyName)
            //    .First(
            //        (prop, next) => prop.)

            return (this.VariableName, PropertyName);
        }
    }

}
