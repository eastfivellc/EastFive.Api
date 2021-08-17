using EastFive.Api.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineWorkflowVariable
    {
        (string, string) GetNameAndValue(Resources.Response response, Method method);
    }

    public class WorkflowVariableAttribute
        : System.Attribute, IDefineWorkflowVariable
    {
        public string VariableName { get; set; }

        public string PropertyName { get; set; }

        public WorkflowVariableAttribute(string variableName, string propertyName)
        {
            this.VariableName = variableName;
            this.PropertyName = propertyName;
        }

        public (string, string) GetNameAndValue(Resources.Response response, Method method)
        {
            //method.Route.Properties
            //    .Where(prop => prop.Name == this.PropertyName)
            //    .First(
            //        (prop, next) => prop.)

            return (this.VariableName, PropertyName);
        }
    }

    public class WorkflowVariable2Attribute : WorkflowVariableAttribute
    {
        public WorkflowVariable2Attribute(string variableName, string propertyName)
            : base(variableName, propertyName)
        {

        }
    }

    public class WorkflowVariable3Attribute : WorkflowVariableAttribute
    {
        public WorkflowVariable3Attribute(string variableName, string propertyName)
            : base(variableName, propertyName)
        {

        }
    }
}
