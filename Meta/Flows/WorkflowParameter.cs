using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive;
using EastFive.Extensions;
using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using EastFive.Linq;


namespace EastFive.Api.Meta.Flows
{
    public abstract class WorkflowParameterBaseAttribute : System.Attribute,
        IDefineWorkflowRequestProperty,
        IDefineQueryItem,
        IDefineWorkflowRequestPropertyFormData
    {
        public string Scope { get; set; }

        public string Description { get; set; }

        public bool Disabled { get; set; } = false;

        protected abstract string GetValue(ParameterInfo parameter, out bool quoted);

        protected virtual string GetDescription(ParameterInfo parameter)
        {
            if (Description.HasBlackSpace())
                return this.Description;

            if (!parameter.ContainsCustomAttribute<System.ComponentModel.DescriptionAttribute>())
                return default;

            var descrAttr = parameter.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            return descrAttr.Description;
        }

        protected virtual bool IsFileType(ParameterInfo parameter) => false;

        public void AddProperties(JsonWriter requestObj, ParameterInfo parameter)
        {
            if (parameter.ContainsAttributeInterface<IBindJsonApiValue>(inherit: true))
            {
                var propertyName = parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                    apiBinder.GetKey(parameter)
                    :
                    parameter.Name;
                requestObj.WritePropertyName(propertyName);

                var value = GetValue(parameter, out bool quoted);
                if (quoted)
                    requestObj.WriteValue(value);
                else
                    requestObj.WriteRawValue(value);

                var description = GetDescription(parameter);
                if (description.HasBlackSpace())
                    requestObj.WriteComment(description);
            }
        }

        public FormData[] GetFormData(ParameterInfo parameter)
        {
            if (!parameter.ContainsAttributeInterface<IBindFormDataApiValue>(inherit: true))
                if (!parameter.ContainsAttributeInterface<IBindMultipartApiValue>(inherit: true))
                    return new FormData[] { };

            var propertyName = parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                apiBinder.GetKey(parameter)
                :
                parameter.Name;

            var value = GetValue(parameter, out bool quoted);
            var description = GetDescription(parameter);
            var isFileType = IsFileType(parameter);
            return new FormData[]
            {
                new FormData
                {
                    key = propertyName,
                    value = value,
                    type = isFileType ? "file" : "text",
                    description = description,
                    disabled = this.Disabled,
                }
            };
        }

        public QueryItem[] GetQueryItem(Method method, ParameterInfo parameter)
        {
            if (parameter.ContainsAttributeInterface<IBindJsonApiValue>(inherit: true))
                return default;
            if (parameter.ContainsAttributeInterface<IBindFormDataApiValue>(inherit: true))
                return default;
            if (parameter.ContainsAttributeInterface<IBindMultipartApiValue>(inherit: true))
                return default;

            var propertyName = parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                    apiBinder.GetKey(parameter)
                    :
                    parameter.Name;
            var value = GetValue(parameter, out bool quotedIgnored);
            var description = GetDescription(parameter);

            return new QueryItem
            {
                key = propertyName,
                value = value,
                description = description,
                disabled = this.Disabled,
            }.AsArray();
        }

        public QueryItem[] GetQueryItems(Method method)
        {
            return new QueryItem[] { };
        }
    }

    public class WorkflowParameterAttribute : WorkflowParameterBaseAttribute
    {
        public string Value { get; set; }

        public bool Quoted { get; set; } = true;

        protected override string GetValue(ParameterInfo parameter, out bool quoted)
        {
            quoted = this.Quoted;
            return this.Value;
        }

        protected override bool IsFileType(ParameterInfo parameter)
        {
            return parameter.ParameterType == typeof(System.IO.Stream) ||
                parameter.ParameterType.IsSubClassOfGeneric(typeof(System.IO.Stream));
        }
    }

    public class WorkflowNewIdAttribute : WorkflowParameterBaseAttribute
    {
        protected override string GetValue(ParameterInfo parameter, out bool quoted)
        {
            quoted = true;
            return "{{$guid}}";
        }

        protected override string GetDescription(ParameterInfo parameter)
        {
            var desc = base.GetDescription(parameter);
            if (desc.HasBlackSpace())
                return desc;

            if (!parameter.ParameterType.GenericTypeArguments.Any())
                return null;

            var refName = parameter.ParameterType.GenericTypeArguments.First().FullName;
            return $"ID of a {refName}";
        }

        
    }

    public class WorkflowEnumAttribute : WorkflowParameterBaseAttribute
    {
        public string Value { get; set; }

        private string GetOptions(ParameterInfo parameter)
        {
            if (!parameter.ParameterType.IsEnum)
                return $"WARINING {parameter.Member.DeclaringType.FullName}..{parameter.Member.Name}({parameter.Name}) is tagged as Enum workflow but is not an Enum.";

            return Enum.GetNames(parameter.ParameterType)
                .Join(',');
        }

        override protected string GetValue(ParameterInfo parameter, out bool quoted)
        {
            quoted = true;
            if (!parameter.ParameterType.IsEnum)
                return $"WARINING {parameter.Member.DeclaringType.FullName}..{parameter.Member.Name}({parameter.Name}) is tagged as Enum workflow but is not an Enum.";

            if (this.Value.IsNullOrWhiteSpace())
                return null;

            return Enum.GetNames(parameter.ParameterType)
                .Where(name => name == this.Value)
                .First(
                    (name, next) => name,
                    () => $"WARNING:`{this.Value}` is not a valid value.");
        }

        protected override string GetDescription(ParameterInfo parameter)
        {
            var desc = base.GetDescription(parameter);
            var options = GetOptions(parameter);
            if (desc.HasBlackSpace())
                return $"{desc} Select one of [{options}]";
            return options;
        }
    }

    public class WorkflowArrayObjectParameterAttribute : System.Attribute,
        IDefineWorkflowRequestProperty
    {
        public string Scope { get; set; }

        public string Value0 { get; set; }
        public string Value1 { get; set; }
        public string Value2 { get; set; }
        public string Value3 { get; set; }

        public void AddProperties(JsonWriter requestObj, ParameterInfo parameter)
        {
            var propertyName = parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                apiBinder.GetKey(parameter)
                :
                parameter.Name;
            requestObj.WritePropertyName(propertyName);
            requestObj.WriteStartArray();
            if (Value0.HasBlackSpace())
            {
                requestObj.WriteValue(Value0);
            }
            if (Value1.HasBlackSpace())
            {
                requestObj.WriteValue(Value1);
            }
            if (Value2.HasBlackSpace())
            {
                requestObj.WriteValue(Value2);
            }
            if (Value3.HasBlackSpace())
            {
                requestObj.WriteValue(Value3);
            }
            requestObj.WriteEndArray();
        }
    }

    public class WorkflowObjectParameterAttribute : System.Attribute,
        IDefineWorkflowRequestProperty
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

