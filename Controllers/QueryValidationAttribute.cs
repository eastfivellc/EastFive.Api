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
                                    v => Convert(httpApp, parameterRequiringValidation.ParameterType, v,
                                        (vCasted) => onCasted(vCasted),
                                        (why) => onInvalid($"Property {name}:{why}")),
                                    why => onInvalid(why));
                                return (TResult)obj;
                            },
                            () =>
                            {
                                return onInvalid("Inform server developer:" +
                                    $"HttpBodyAttribute on `{method.DeclaringType.FullName}.{method.Name}` resolves to type `{bodyAttr.Type.FullName}` " + 
                                    $"and specifies parameter `{name}` which is not a member of {bodyAttr.Type.FullName}").ToTask();
                            });
                },
                () =>
                {
                    // TODO: Check the FunctionViewController for a type
                    return onInvalid($"{method.DeclaringType.FullName}.{method.Name}'s does not contain a type specifier.").ToTask();
                });
        }

        public virtual TResult Convert<TResult>(HttpApplication httpApp, Type type, object value,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            if (value.IsDefaultOrNull())
            {
                return onCasted(type.GetDefault());
            }

            if (type.IsAssignableFrom(value.GetType()))
                return onCasted(value);

            if (value is BlackBarLabs.Api.Resources.WebId)
            {
                var webId = value as BlackBarLabs.Api.Resources.WebId;
                if (typeof(Guid).GUID == type.GUID)
                {
                    if (webId.IsDefaultOrNull())
                        return onInvalid("Value did not provide a UUID.");
                    return onCasted(webId.UUID);
                }
                if (typeof(Guid?).GUID == type.GUID)
                {
                    if (webId.IsDefaultOrNull())
                        return onCasted(default(Guid?));
                    var valueGuidMaybe = (Guid?)webId.UUID;
                    return onCasted(valueGuidMaybe);
                }
            }
            
            if (value is Guid?)
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

                if (type.IsEnum)
                {
                    if (Enum.IsDefined(type, valueString))
                    {
                        var valueEnum = Enum.Parse(type, valueString);
                        return onCasted(valueEnum);
                    }
                    return onInvalid($"{valueString} is not one of [{Enum.GetNames(type).Join(",")}]");
                }

                if(typeof(Type).GUID == type.GUID)
                {
                    return onCasted(httpApp.GetResourceType(valueString));
                }
            }

            if (value is int)
            {
                if (type.IsEnum)
                {
                    var valueInt = (int)value;
                    var valueEnum = Enum.ToObject(type, valueInt);
                    return onCasted(valueEnum);
                }
            }

            if(value.GetType().IsArray)
            {
                if (type.IsArray)
                {
                    var elementType = type.GetElementType();
                    var array = (object[])value;

                    //var casted = Array.ConvertAll(array,
                    //    item => item.ToString());
                    //var typeConverted = casted.Cast<int>().ToArray();

                    var casted = Array.ConvertAll(array,
                        item => Convert(httpApp, elementType, item, (v) => v, (why) => elementType.GetDefault()));
                    var typeConvertedEnumerable = typeof(System.Linq.Enumerable)
                        .GetMethod("Cast", BindingFlags.Static | BindingFlags.Public)
                        .MakeGenericMethod(new Type[] { elementType })
                        .Invoke(null, new object[] { casted });
                    var typeConvertedArray = typeof(System.Linq.Enumerable)
                        .GetMethod("ToArray", BindingFlags.Static | BindingFlags.Public)
                        .MakeGenericMethod(new Type[] { elementType })
                        .Invoke(null, new object[] { typeConvertedEnumerable });
                    
                    return onCasted(typeConvertedArray);
                }
            }
            
            return onInvalid($"Could not convert `{value.GetType().FullName}` to `{type.FullName}`.");
        }
    }

    public class PropertyOptionalAttribute : PropertyAttribute
    {
        public override async Task<TResult> OnEmptyValueAsync<TResult>(HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterRequiringValidation,
            Func<object, TResult> onValid,
            Func<TResult> onInvalid)
        {
            if (parameterRequiringValidation.ParameterType == typeof(Guid?))
                return onValid(default(Guid?));
            if (parameterRequiringValidation.ParameterType == typeof(DateTime?))
                return onValid(default(DateTime?));
            if (parameterRequiringValidation.ParameterType == typeof(string))
                return onValid(default(string));
            if (parameterRequiringValidation.ParameterType.IsArray)
                return onValid(null);

            if (!parameterRequiringValidation.ParameterType.IsEnum)
                return parameterRequiringValidation.ParameterType.IsNullable(
                    underlyingType =>
                    {
                        return onValid(parameterRequiringValidation.ParameterType.GetDefault());
                    },
                    () => onValid(Activator.CreateInstance(parameterRequiringValidation.ParameterType)));
            
            return await onInvalid().ToTask();
        }
    }

    public class PropertyGuidAttribute : PropertyAttribute
    {
        
    }

    public class PropertyEnumAttribute : PropertyAttribute
    {
    }
}
