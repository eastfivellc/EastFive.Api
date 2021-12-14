using System;
using System.Reflection;

namespace EastFive.Api.Meta.Flows
{
    public class WorkflowParameterFromVariableAttribute : WorkflowParameterBaseAttribute
    {
        public string Value { get; set; }

        protected override string GetValue(ParameterInfo parameter) => $"{{{{{this.Value}}}}}";
    }
}

