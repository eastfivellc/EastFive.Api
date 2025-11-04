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
using EastFive.Web.Configuration;


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

    public interface IDefineWorkflowParameterAttributes
    {
        bool IsFileType(ParameterInfo parameter);
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
            if (parameter.ParameterType == typeof(System.IO.Stream))
                return true;
            if(parameter.ParameterType.IsSubClassOfGeneric(typeof(System.IO.Stream)))
                return true;

            if (!parameter.ParameterType.TryGetAttributeInterface(
                out IDefineWorkflowParameterAttributes defineWorkflowParameterAttributes))
                return false;

            return defineWorkflowParameterAttributes.IsFileType(parameter);
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
        public string AppSettingKey0 { get; set; }
        public string Value0 { get; set; }
        public string AppSettingValue0 { get; set; }

        public string Key1 { get; set; }
        public string AppSettingKey1 { get; set; }
        public string Value1 { get; set; }
        public string AppSettingValue1 { get; set; }

        public string Key2 { get; set; }
        public string AppSettingKey2 { get; set; }
        public string Value2 { get; set; }
        public string AppSettingValue2 { get; set; }

        public string Key3 { get; set; }
        public string AppSettingKey3 { get; set; }
        public string Value3 { get; set; }
        public string AppSettingValue3 { get; set; }


        public void AddProperties(JsonWriter requestObj, ParameterInfo parameter)
        {
            var propertyName = parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                apiBinder.GetKey(parameter)
                :
                parameter.Name;
            requestObj.WritePropertyName(propertyName);
            requestObj.WriteStartObject();

            WriteProperty(Key0, AppSettingKey0, Value0, AppSettingValue0);
            WriteProperty(Key1, AppSettingKey1, Value1, AppSettingValue1);
            WriteProperty(Key2, AppSettingKey2, Value2, AppSettingValue2);
            WriteProperty(Key3, AppSettingKey3, Value3, AppSettingValue3);

            requestObj.WriteEndObject();

            void WriteProperty(string key, string appSettingKey, string value, string appSettingValue)
            {
                if (key.HasBlackSpace())
                {
                    requestObj.WritePropertyName(key);
                    WriteValue();
                    return;
                }

                if (appSettingKey.HasBlackSpace())
                {
                    _ = appSettingKey.ConfigurationString(
                        appSettingExtractedKey =>
                        {
                            requestObj.WritePropertyName(appSettingExtractedKey);
                            WriteValue();
                            return true;
                        },
                        (why) =>
                        {
                            return false;
                        });
                }
                
                void WriteValue()
                {
                    if (appSettingValue.HasBlackSpace())
                    {
                        var didExtract = appSettingValue.ConfigurationString(
                            appSettingExtractedValue =>
                            {
                                requestObj.WriteValue(appSettingExtractedValue);
                                return true;
                            },
                            (why) =>
                            {
                                requestObj.WriteNull();
                                return false;
                            });
                        return;
                    }
                    
                    requestObj.WriteValue(value);
                }
            }
        }
    }
}

