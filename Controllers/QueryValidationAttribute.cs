using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Extensions;
using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class QueryDefaultParameterAttribute : System.Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class QueryValidationAttribute : System.Attribute
    {
        public string Name { get; set; }

        public delegate Task<object> CastDelegate(Type type,
            Func<object, object> onCasted,
            Func<string, object> onFailedToCast);

        public virtual async Task<TResult> TryCastAsync<TResult>(HttpApplication httpApp, HttpRequestMessage request,
                MethodInfo method, ParameterInfo parameterRequiringValidation, CastDelegate fetch,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            var obj = await fetch(parameterRequiringValidation.ParameterType,
                v => onCasted(v),
                why => onInvalid(why));
            return (TResult)obj;
        }

        public virtual Task<TResult> OnEmptyValueAsync<TResult>(HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterRequiringValidation,
            Func<object, TResult> onValid,
            Func<TResult> onInvalid)
        {
            return onInvalid().ToTask();
        }
    }

    public class RequiredAttribute : QueryValidationAttribute
    {
    }

    public class RequiredAndAvailableInPathAttribute : QueryValidationAttribute
    {
    }

    public class OptionalAttribute : QueryValidationAttribute
    {
        public override Task<TResult> OnEmptyValueAsync<TResult>(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterRequiringValidation,
            Func<object, TResult> onValid,
            Func<TResult> onInvalid)
        {
            return onValid(parameterRequiringValidation.ParameterType.GetDefault()).ToTask();
        }
    }

    public class PropertyAttribute : QueryValidationAttribute
    {
        public override Task<TResult> TryCastAsync<TResult>(HttpApplication httpApp, HttpRequestMessage request,
                MethodInfo method, ParameterInfo parameterRequiringValidation, CastDelegate fetch,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            return method.GetCustomAttribute<HttpBodyAttribute, Task<TResult>>(
                bodyAttr =>
                {
                    if (bodyAttr.Type.IsDefaultOrNull())
                        return onInvalid($"Cannot determine property type for parameter: {method.DeclaringType.FullName}.{method.Name}({parameterRequiringValidation.ParameterType.FullName} {parameterRequiringValidation.Name}).").ToTask();
                    var name = this.Name.IsNullOrWhiteSpace() ? parameterRequiringValidation.Name : this.Name;
                    return bodyAttr.Type.GetProperties()
                        .First(
                            async (prop, next) =>
                            {
                                var propertyName = prop.GetCustomAttribute<Newtonsoft.Json.JsonPropertyAttribute, string>(
                                    attr => attr.PropertyName,
                                    () => prop.Name);

                                if (propertyName != name)
                                    return await next();
                                var obj = await fetch(prop.PropertyType,
                                    v => Convert(parameterRequiringValidation.ParameterType, v,
                                        (vCasted) => onCasted(vCasted),
                                        (why) => onInvalid($"Property {name}:{why}")),
                                    why => onInvalid(why));
                                return (TResult)obj;
                            },
                            () => onInvalid($"No match for property {name}").ToTask());
                },
                () =>
                {
                    // TODO: Check the FunctionViewController for a type
                    return onInvalid($"{method.DeclaringType.FullName}.{method.Name}'s does not contain a type specifier.").ToTask();
                });
        }

        public virtual TResult Convert<TResult>(Type type, object value,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            if(value is Guid?)
            {
                var guidMaybe = value as Guid?;
                if (typeof(Guid).GUID == type.GUID)
                {
                    if (!guidMaybe.HasValue)
                        return onInvalid("Value did not provide a UUID.");
                    return onCasted(guidMaybe.Value);
                }
                if (typeof(Guid?).GUID == type.GUID)
                {
                    return onCasted(guidMaybe);
                }
            }
            if (value is string)
            {
                var valueString = value as string;
                if (typeof(Guid).GUID == type.GUID)
                {
                    if (Guid.TryParse(valueString, out Guid valueGuid))
                        return onCasted(valueGuid);
                    return onInvalid($"[{valueString}] is not a valid UUID.");
                }
                if (typeof(Guid?).GUID == type.GUID)
                {
                    if (valueString.IsNullOrWhiteSpace())
                        return onCasted(default(Guid?));
                    if (Guid.TryParse(valueString, out Guid valueGuid))
                        return onCasted(valueGuid);
                    return onInvalid($"[{valueString}] needs to be empty or a valid UUID.");
                }
                if (typeof(DateTime).GUID == type.GUID)
                {
                    if (DateTime.TryParse(valueString, out DateTime valueDateTime))
                        return onCasted(valueDateTime);
                    return onInvalid($"[{valueString}] needs to be a valid date/time.");
                }
                if (typeof(DateTime?).GUID == type.GUID)
                {
                    if (valueString.IsNullOrWhiteSpace())
                        return onCasted(default(DateTime?));
                    if (DateTime.TryParse(valueString, out DateTime valueDateTime))
                        return onCasted(valueDateTime);
                    return onInvalid($"[{valueString}] needs to be empty or a valid date/time.");
                }
            }

            return onCasted(value);
        }
    }

    public class PropertyGuidAttribute : PropertyAttribute
    {
        public override TResult Convert<TResult>(Type type, object value,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            if (typeof(Guid).GUID == type.GUID)
            {
                if (value is Guid)
                {
                    var valueGuid = (Guid)value;
                    return onCasted(valueGuid);
                }

                if (value is Guid?)
                {
                    var valueGuidMaybe = (Guid?)value;
                    if (!valueGuidMaybe.HasValue)
                        return onInvalid("A value is required.");
                    return onCasted(valueGuidMaybe);
                }

                if (value is BlackBarLabs.Api.Resources.WebId)
                {
                    var webId = value as BlackBarLabs.Api.Resources.WebId;

                    var guidMaybe = webId.ToGuid();
                    if (!guidMaybe.HasValue)
                        return onInvalid("Value did not provide a UUID.");
                    return onCasted(guidMaybe.Value);
                }

                if (value is string)
                {
                    var valueString = value as string;
                    if (Guid.TryParse(valueString, out Guid valueGuid))
                        return onCasted(valueGuid);
                }

                return onInvalid($"PropertyGuid could not cast {value.GetType().FullName} to {type.FullName}");
            }

            if (typeof(Guid?).GUID == type.GUID)
            {
                if (value is Guid)
                {
                    var valueGuid = (Guid)value;
                    var valueGuidMaybe = (Guid?)valueGuid;
                    return onCasted(valueGuidMaybe);
                }

                if (value is Guid?)
                {
                    var valueGuidMaybe = (Guid?)value;
                    return onCasted(valueGuidMaybe);
                }
                
                if (value.IsDefaultOrNull())
                {
                    var valueGuidMaybe = default(Guid?);
                    return onCasted(valueGuidMaybe);
                }

                if (value is BlackBarLabs.Api.Resources.WebId)
                {
                    var webId = value as BlackBarLabs.Api.Resources.WebId;
                    var guidMaybe = webId.ToGuid();
                    return onCasted(guidMaybe);
                }

                if (value is string)
                {
                    var valueString = value as string;
                    if (Guid.TryParse(valueString, out Guid valueGuid))
                    {
                        var valueGuidMaybe = (Guid?)valueGuid;
                        return onCasted(valueGuidMaybe);
                    }
                }
            }
            
            return onInvalid($"PropertyGuid could not cast {value.GetType().FullName} to {type.FullName}");
        }
    }

    public class PropertyEnumAttribute : PropertyAttribute
    {
        public override TResult Convert<TResult>(Type type, object value,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            if (value.GetType().GUID == type.GUID)
                return onCasted(value);

            if (!type.IsEnum)
                return type.IsNullable(
                    underlyingType =>
                    {
                        if (value.IsDefaultOrNull())
                            return onCasted(type.GetDefault());
                        return Convert(underlyingType, value,
                            underlyingValue => onCasted(Activator.CreateInstance(type, underlyingValue)),
                            onInvalid);
                    },
                    () => onInvalid($"PropertyEnum is not a valid query validation for type {type.FullName}"));

            if (value is int)
            {
                var valueInt = (int)value;
                var valueEnum = Enum.ToObject(type, valueInt);
                return onCasted(valueEnum);
            }

            if (value is string)
            {
                var valueString = value as string;
                if(Enum.IsDefined(type, valueString))
                {
                    var valueEnum = Enum.Parse(type, valueString);
                    return onCasted(valueEnum);
                }
                return onInvalid($"{valueString} is not one of [{Enum.GetNames(type).Join(",")}]");
            }

            return onInvalid($"PropertyEnum could not cast {value.GetType().FullName} to {type.FullName}");
        }
    }

    public class PropertyStringAttribute : PropertyAttribute
    {
        public override TResult Convert<TResult>(Type type, object value,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            if (value.GetType().GUID == type.GUID)
                return onCasted(value);

            if (value is string && type.IsAssignableFrom(typeof(string)))
                return onCasted(value);

            return onInvalid($"PropertyString could not cast {value.GetType().FullName} to {type.FullName}");
        }
    }
}
