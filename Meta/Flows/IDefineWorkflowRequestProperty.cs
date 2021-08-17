using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineWorkflowRequestProperty
    {
        public string Scope { get; }

        void AddProperties(JsonWriter requestObj, ParameterInfo parameter);
    }

    public class WorkflowParameterAttribute : System.Attribute, IDefineWorkflowRequestProperty
    {
        public string Value { get; set; }
        public string Scope { get; set; }

        public void AddProperties(JsonWriter requestObj, ParameterInfo parameter)
        {
            var propertyName = parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                apiBinder.GetKey(parameter)
                :
                parameter.Name;
            requestObj.WritePropertyName(propertyName);
            requestObj.WriteValue(this.Value);
        }
    }

    public class WorkflowNewIdAttribute : System.Attribute, IDefineWorkflowRequestProperty
    {
        public string Scope { get; set; }

        public void AddProperties(JsonWriter requestObj, ParameterInfo parameter)
        {
            var propertyName = parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                apiBinder.GetKey(parameter)
                :
                parameter.Name;
            requestObj.WritePropertyName(propertyName);
            requestObj.WriteValue("{{$guid}}");
        }
    }
}
