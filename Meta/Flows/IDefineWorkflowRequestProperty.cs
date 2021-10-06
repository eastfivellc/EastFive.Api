using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
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

    public class WorkflowParameterAttribute : System.Attribute, IDefineWorkflowRequestProperty, IDefineQueryItem
    {
        public string Value { get; set; }
        public string Scope { get; set; }

        public void AddProperties(JsonWriter requestObj, ParameterInfo parameter)
        {
            if (parameter.ContainsAttributeInterface<IBindJsonApiValue>(inherit: true))
            {
                var propertyName = parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                    apiBinder.GetKey(parameter)
                    :
                    parameter.Name;
                requestObj.WritePropertyName(propertyName);
                requestObj.WriteValue(this.Value);
            }
        }

        public QueryItem? GetQueryItem(Method method, ParameterInfo parameter)
        {
            if (parameter.ContainsAttributeInterface<IBindJsonApiValue>(inherit: true))
                return default;

            var propertyName = parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                    apiBinder.GetKey(parameter)
                    :
                    parameter.Name;
            return new QueryItem
            {
                key = propertyName,
                value = this.Value,
            };
        }

        public QueryItem[] GetQueryItems(Method method)
        {
            return new QueryItem[] { };
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

    public class WorkflowObjectParameterAttribute : System.Attribute, IDefineWorkflowRequestProperty
    {
        public string Scope { get; set; }

        public string Key0 { get; set; }
        public string Value0 { get; set; }

        public string Key1 { get; set; }
        public string Value1 { get; set; }

        public string Key2 { get; set; }
        public string Value2 { get; set; }

        public string Key3 { get; set; }
        public string Value3 { get; set; }

        public void AddProperties(JsonWriter requestObj, ParameterInfo parameter)
        {
            var propertyName = parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                apiBinder.GetKey(parameter)
                :
                parameter.Name;
            requestObj.WritePropertyName(propertyName);
            requestObj.WriteStartObject();
            if (Key0.HasBlackSpace())
            {
                requestObj.WritePropertyName(Key0);
                requestObj.WriteValue(Value0);
            }
            if (Key1.HasBlackSpace())
            {
                requestObj.WritePropertyName(Key1);
                requestObj.WriteValue(Value1);
            }
            if (Key2.HasBlackSpace())
            {
                requestObj.WritePropertyName(Key2);
                requestObj.WriteValue(Value2);
            }
            if (Key3.HasBlackSpace())
            {
                requestObj.WritePropertyName(Key3);
                requestObj.WriteValue(Value3);
            }
            requestObj.WriteEndObject();
        }
    }
}
