using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
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
    public class QueryValidationAttribute : System.Attribute, IProvideApiValue
    {
        public string Name { get; set; }
        
        public virtual async Task<TResult> TryCastAsync<TResult>(HttpApplication httpApp,
                HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
                Api.CastDelegate<TResult> fetchQueryParam,
                Api.CastDelegate<TResult> fetchBodyParam,
                Api.CastDelegate<TResult> fetchDefaultParam,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            var found = false;
            var queryResult = await fetchQueryParam(this.Name, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    found = true;
                    return onCasted(v);
                },
                (whyQuery) => default(TResult));
            if (found)
                return queryResult;
            
            var  bodyResult = await fetchBodyParam(this.Name, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    found = true;
                    return onCasted(v);
                },
                (whyQuery) => default(TResult));
            if (found)
                return bodyResult;


            return await fetchDefaultParam(this.Name, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    found = true;
                    return onCasted(v);
                },
                (whyQuery) => onInvalid($"Could not create value in query, body, or file."));
        }
        
    }

    public class RequiredAttribute : QueryValidationAttribute
    {
    }

    public class QueryParameterAttribute : QueryValidationAttribute
    {
        public bool CheckFileName { get; set; }

        protected string GetQueryParameterName(ParameterInfo parameterRequiringValidation)
        {
            if (this.Name.IsNullOrWhiteSpace())
                return parameterRequiringValidation.Name.ToLower();
            return this.Name.ToLower();
        }

        public override async Task<TResult> TryCastAsync<TResult>(HttpApplication httpApp, 
                HttpRequestMessage request, MethodInfo method, 
                ParameterInfo parameterRequiringValidation,
                CastDelegate<TResult> fetchQueryParam,
                CastDelegate<TResult> fetchBodyParam, 
                CastDelegate<TResult> fetchDefaultParam,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            bool found = false;
            var queryName = GetQueryParameterName(parameterRequiringValidation);
            var queryResult = await fetchQueryParam(queryName, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    found = true;
                    return onCasted(v);
                },
                (whyQuery) => default(TResult));
            if (found)
                return queryResult;

            if(!CheckFileName)
                return onInvalid($"Query parameter `${queryName}` was not specified in the request query.");

            return await fetchDefaultParam(queryName, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    found = true;
                    return onCasted(v);
                },
                (whyQuery) => onInvalid($"Query parameter `${queryName}` was not specified in the request query or filename."));
        }
    }

    public class OptionalQueryParameterAttribute : QueryParameterAttribute
    {
        public override Task<TResult> TryCastAsync<TResult>(HttpApplication httpApp, 
                HttpRequestMessage request, MethodInfo method,
                ParameterInfo parameterRequiringValidation, 
                CastDelegate<TResult> fetchQueryParam, 
                CastDelegate<TResult> fetchBodyParam, 
                CastDelegate<TResult> fetchDefaultParam, 
            Func<object, TResult> onCasted, 
            Func<string, TResult> onInvalid)
        {
            return base.TryCastAsync(httpApp, request, method, parameterRequiringValidation, fetchQueryParam, fetchBodyParam, fetchDefaultParam,
                onCasted,
                (why) =>
                {
                    return onCasted(parameterRequiringValidation.ParameterType.GetDefault());
                });
        }
    }

    public class ResourceAttribute : QueryValidationAttribute
    {
        public override Task<TResult> TryCastAsync<TResult>(HttpApplication httpApp, 
            HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate<TResult> fetchQueryParam, 
            CastDelegate<TResult> fetchBodyParam,
            CastDelegate<TResult> fetchDefaultParam,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            return fetchBodyParam(string.Empty, parameterRequiringValidation.ParameterType,
                onCasted,
                onInvalid);
        }
    }


    public class PropertyAttribute : QueryValidationAttribute
    {
        public override Task<TResult> TryCastAsync<TResult>(HttpApplication httpApp,
                HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
                CastDelegate<TResult> fetchQueryParam,
                CastDelegate<TResult> fetchBodyParam,
                CastDelegate<TResult> fetchDefaultParam,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            return method.GetCustomAttribute<HttpBodyAttribute, Task<TResult>>(
                bodyAttr =>
                {
                    var type = bodyAttr.Type;
                    if (type.IsDefaultOrNull())
                    {
                        type = method.DeclaringType.GetCustomAttribute<FunctionViewControllerAttribute, Type>(
                            fvcAttr => fvcAttr.Resource,
                            () => type);

                        if (type.IsDefaultOrNull())
                            return onInvalid($"Cannot determine property type for method: {method.DeclaringType.FullName}.{method.Name}().").ToTask();
                    }
                    var name = this.Name.IsNullOrWhiteSpace() ? parameterRequiringValidation.Name : this.Name;
                    return type
                        .GetProperties()
                        .Concat<MemberInfo>(type.GetFields())
                        .First(
                            async (prop, next) =>
                            {
                                var propertyName = prop.GetCustomAttribute<Newtonsoft.Json.JsonPropertyAttribute, string>(
                                    attr => attr.PropertyName,
                                    () => prop.Name);

                                if (propertyName != name)
                                    return await next();
                                var obj = await fetchBodyParam(propertyName, prop.GetPropertyOrFieldType(),
                                    v => Convert(httpApp, parameterRequiringValidation.ParameterType, v,
                                        (vCasted) => onCasted(vCasted),
                                        (why) => onInvalid($"Property {name}:{why}")),
                                    why => onInvalid(why));
                                return (TResult)obj;
                            },
                            () =>
                            {
                                return onInvalid("Inform server developer:" +
                                    $"HttpBodyAttribute on `{method.DeclaringType.FullName}.{method.Name}` resolves to type `{type.FullName}` " + 
                                    $"and specifies parameter `{name}` which is not a member of {type.FullName}").ToTask();
                            });
                },
                () =>
                {
                    // TODO: Check the FunctionViewController for a type
                    return onInvalid($"{method.DeclaringType.FullName}.{method.Name}'s does not contain a type specifier.").AsTask();
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
        public override Task<TResult> TryCastAsync<TResult>(
                HttpApplication httpApp, HttpRequestMessage request, MethodInfo method,
                ParameterInfo parameterRequiringValidation,
                CastDelegate<TResult> fetchQueryParam, CastDelegate<TResult> fetchBodyParam, CastDelegate<TResult> fetchDefaultParam, 
            Func<object, TResult> onValid, 
            Func<string, TResult> onInvalid)
        {
            return base.TryCastAsync(httpApp, request, method, parameterRequiringValidation,
                fetchQueryParam, fetchBodyParam, fetchDefaultParam,
                onValid,
                (discardWhy) =>
                {
                    if (parameterRequiringValidation.ParameterType == typeof(Guid?))
                        return onValid(default(Guid?));
                    if (parameterRequiringValidation.ParameterType == typeof(DateTime?))
                        return onValid(default(DateTime?));
                    if (parameterRequiringValidation.ParameterType == typeof(string))
                        return onValid(default(string));
                    if (parameterRequiringValidation.ParameterType.IsArray)
                        return onValid(null);

                    return parameterRequiringValidation.ParameterType.IsNullable(
                            underlyingType =>
                            {
                                return onValid(parameterRequiringValidation.ParameterType.GetDefault());
                            },
                            () =>
                            {
                                // enum and interfaces cannot be actived
                                if (parameterRequiringValidation.ParameterType.IsEnum)
                                    return onValid(parameterRequiringValidation.ParameterType.GetDefault());
                                if (parameterRequiringValidation.ParameterType.IsInterface)
                                    return onValid(parameterRequiringValidation.ParameterType.GetDefault());

                                var instance = Activator.CreateInstance(parameterRequiringValidation.ParameterType);
                                return onValid(instance);
                            });
                });
            
        }
    }
}
