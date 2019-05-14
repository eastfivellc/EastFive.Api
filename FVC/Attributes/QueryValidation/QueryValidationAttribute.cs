using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;

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

        public RequestMessage<TResource> BindContent<TResource>(RequestMessage<TResource> request,
            MethodInfo method, ParameterInfo parameter, object resource)
        {
            throw new NotImplementedException();
        }

        public virtual async Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
                HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
                Api.CastDelegate<SelectParameterResult> fetchQueryParam,
                Api.CastDelegate<SelectParameterResult> fetchBodyParam,
                Api.CastDelegate<SelectParameterResult> fetchDefaultParam)
        {
            var found = false;
            var queryResult = await fetchQueryParam(this.Name, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    found = true;
                    return new SelectParameterResult
                    {

                    };
                },
                (whyQuery) => SelectParameterResult.Failure(whyQuery, this.Name, parameterRequiringValidation));
            if (found)
                return queryResult;
            
            var  bodyResult = await fetchBodyParam(this.Name, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    found = true;
                    return new SelectParameterResult(v, this.Name, parameterRequiringValidation);
                },
                (whyQuery) => SelectParameterResult.Failure(whyQuery, this.Name, parameterRequiringValidation));
            if (found)
                return bodyResult;


            return await fetchDefaultParam(this.Name, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    found = true;
                    return new SelectParameterResult(v, this.Name, parameterRequiringValidation);
                },
                (whyQuery) => SelectParameterResult.Failure($"Could not create value in query, body, or file.", this.Name, parameterRequiringValidation));
        }

        
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

        public override async Task<SelectParameterResult> TryCastAsync(IApplication httpApp, 
                HttpRequestMessage request, MethodInfo method, 
                ParameterInfo parameterRequiringValidation,
                CastDelegate<SelectParameterResult> fetchQueryParam,
                CastDelegate<SelectParameterResult> fetchBodyParam, 
                CastDelegate<SelectParameterResult> fetchDefaultParam)
        { 
            bool found = false;
            var queryName = GetQueryParameterName(parameterRequiringValidation);
            var queryResult = await fetchQueryParam(queryName, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    found = true;
                    return new SelectParameterResult(v, queryName, parameterRequiringValidation);
                },
                (whyQuery) => new SelectParameterResult());
            if (found)
                return queryResult;

            if (!CheckFileName)
                return SelectParameterResult.Failure($"Query parameter `{queryName}` was not specified in the request query.", queryName, parameterRequiringValidation);

            return await fetchDefaultParam(queryName, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    found = true;
                    return new SelectParameterResult(v, queryName, parameterRequiringValidation);
                },
                (whyQuery) => SelectParameterResult.Failure($"Query parameter `{queryName}` was not specified in the request query or filename.", queryName, parameterRequiringValidation));
        }
    }

    public class OptionalQueryParameterAttribute : QueryParameterAttribute
    {
        public async override Task<SelectParameterResult> TryCastAsync(IApplication httpApp, 
                HttpRequestMessage request, MethodInfo method,
                ParameterInfo parameterRequiringValidation, 
                CastDelegate<SelectParameterResult> fetchQueryParam, 
                CastDelegate<SelectParameterResult> fetchBodyParam, 
                CastDelegate<SelectParameterResult> fetchDefaultParam)
        {
            var baseValue = await base.TryCastAsync(httpApp, request, method,
                parameterRequiringValidation,
                fetchQueryParam, fetchBodyParam, fetchDefaultParam);
            if (!baseValue.valid)
            {
                baseValue.value = parameterRequiringValidation.ParameterType.GetDefault();
                baseValue.valid = true;
            }
            return baseValue;
        }
    }

    public class UpdateIdAttribute : QueryParameterAttribute
    {
        public async override Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
                HttpRequestMessage request, MethodInfo method,
                ParameterInfo parameterRequiringValidation,
                CastDelegate<SelectParameterResult> fetchQueryParam,
                CastDelegate<SelectParameterResult> fetchBodyParam,
                CastDelegate<SelectParameterResult> fetchDefaultParam)
        {
            base.CheckFileName = true;
            var baseValue = await base.TryCastAsync(httpApp, request, method, parameterRequiringValidation, fetchQueryParam, fetchBodyParam, fetchDefaultParam);
            if (baseValue.valid)
                return baseValue;

            var queryName = GetQueryParameterName(parameterRequiringValidation);
            return await fetchBodyParam(queryName, parameterRequiringValidation.ParameterType,
                vCasted => SelectParameterResult.Body(vCasted, queryName, parameterRequiringValidation),
                why => SelectParameterResult.Failure(why, queryName, parameterRequiringValidation));
        }
    }

    public class HeaderAttribute : QueryValidationAttribute
    {
        public string Content { get; set; }

        public override async Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
            HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate<SelectParameterResult> fetchQueryParam,
            CastDelegate<SelectParameterResult> fetchBodyParam,
            CastDelegate<SelectParameterResult> fetchDefaultParam)
        {
            var bindType = parameterRequiringValidation.ParameterType;
            if(typeof(System.Net.Http.Headers.MediaTypeHeaderValue) == bindType)
            {
                if(Content.IsNullOrWhiteSpace())
                    return new SelectParameterResult
                    {
                        valid = true,
                        fromBody = false,
                        fromQuery = false,
                        key = Content,
                        parameterInfo = parameterRequiringValidation,
                        value = request.Content.Headers.ContentType.MediaType,
                    };
                return await fetchBodyParam(this.Content, typeof(HttpContent),
                    (parts) =>
                    {
                        var content = parts as HttpContent;
                        return new SelectParameterResult
                        {
                            valid = true,
                            fromBody = false,
                            fromQuery = false,
                            key = Content,
                            parameterInfo = parameterRequiringValidation,
                            value = content.Headers.ContentType,
                        };
                    },
                    (why) =>
                    {
                        return new SelectParameterResult
                        {
                            fromBody = false,
                            key = "",
                            fromQuery = false,
                            parameterInfo = parameterRequiringValidation,
                            valid = false,
                            failure = why, // $"Cannot extract MediaTypeHeaderValue from non-multipart request.",
                        };
                    });
            }
            if (bindType.IsSubClassOfGeneric(typeof(HttpHeaderValueCollection<>)))
            {
                if (bindType.GenericTypeArguments.First() == typeof(StringWithQualityHeaderValue))
                {
                    if (!Content.IsNullOrWhiteSpace())
                        return new SelectParameterResult
                        {
                            fromBody = false,
                            key = "",
                            fromQuery = false,
                            parameterInfo = parameterRequiringValidation,
                            valid = false,
                            failure = "AcceptLanguage is not a content header.",
                        };
                    return new SelectParameterResult
                    {
                        valid = true,
                        fromBody = false,
                        fromQuery = false,
                        key = Content,
                        parameterInfo = parameterRequiringValidation,
                        value = request.Headers.AcceptLanguage,
                    };
                }
            }
            return new SelectParameterResult
            {
                fromBody = false,
                key = "",
                fromQuery = false,
                parameterInfo = parameterRequiringValidation,
                valid = false,
                failure = $"No header binding for type `{bindType.FullName}`.",
            };
        }
    }

    public class PropertyAttribute : QueryValidationAttribute
    {
        public override Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
                HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
                CastDelegate<SelectParameterResult> fetchQueryParam,
                CastDelegate<SelectParameterResult> fetchBodyParam,
                CastDelegate<SelectParameterResult> fetchDefaultParam)
        {
            var name = this.Name.IsNullOrWhiteSpace() ? parameterRequiringValidation.Name : this.Name;
            return fetchBodyParam(name, parameterRequiringValidation.ParameterType,
                vCasted => SelectParameterResult.Body(vCasted, name, parameterRequiringValidation),
                why => SelectParameterResult.Failure(why, name, parameterRequiringValidation));

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
                    return httpApp.GetResourceType(valueString,
                            (typeInstance) => onCasted(typeInstance),
                            () => valueString.GetClrType(
                                typeInstance => onCasted(typeInstance),
                                () => onInvalid(
                                    $"`{valueString}` is not a recognizable resource type or CLR type.")));
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
        public async override Task<SelectParameterResult> TryCastAsync(
                IApplication httpApp, HttpRequestMessage request, MethodInfo method,
                ParameterInfo parameterRequiringValidation,
                CastDelegate<SelectParameterResult> fetchQueryParam, 
                CastDelegate<SelectParameterResult> fetchBodyParam, 
                CastDelegate<SelectParameterResult> fetchDefaultParam)
        {
            var baseValue = await base.TryCastAsync(httpApp, request, method, parameterRequiringValidation, fetchQueryParam, fetchBodyParam, fetchDefaultParam);
            if (baseValue.valid)
                return baseValue;

            var parameterType = parameterRequiringValidation.ParameterType;
            if (parameterType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
            {
                var refType = parameterType.GenericTypeArguments.First();
                var parameterTypeGeneric = typeof(RefOptional<>).MakeGenericType(new Type[] { refType });
                baseValue.value = Activator.CreateInstance(parameterTypeGeneric, new object[] { });
                baseValue.valid = true;
                return baseValue;
            }

            if (parameterType.IsSubClassOfGeneric(typeof(IRefs<>)))
            {
                var refType = parameterType.GenericTypeArguments.First();
                var parameterTypeGeneric = typeof(Refs<>).MakeGenericType(new Type[] { refType });
                var refIds = new Guid[] { };
                var refIdsLookupType = typeof(Func<,>).MakeGenericType(new Type[] { typeof(Guid), refType });
                var refIdsLookup = refIdsLookupType.GetDefault();
                baseValue.value = Activator.CreateInstance(
                    parameterTypeGeneric, new object[] { refIds });
                baseValue.valid = true;
                return baseValue;
            }

            if (parameterType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                var parameterTypeGeneric = typeof(Dictionary<,>).MakeGenericType(parameterType.GenericTypeArguments);
                baseValue.value = Activator.CreateInstance(parameterTypeGeneric);
                baseValue.valid = true;
                return baseValue;
            }

            baseValue.value = parameterType.GetDefault();
            baseValue.valid = true;
            return baseValue;

            //return base.TryCastAsync(httpApp, request, method, parameterRequiringValidation,
            //    fetchQueryParam, fetchBodyParam, fetchDefaultParam,
            //    onValid,
            //    (discardWhy) =>
            //    {
            //        if (parameterRequiringValidation.ParameterType == typeof(Guid?))
            //            return onValid(default(Guid?), null);
            //        if (parameterRequiringValidation.ParameterType == typeof(DateTime?))
            //            return onValid(default(DateTime?), null);
            //        if (parameterRequiringValidation.ParameterType == typeof(string))
            //            return onValid(default(string), null);
            //        if (parameterRequiringValidation.ParameterType.IsArray)
            //            return onValid(null, null);

            //        return parameterRequiringValidation.ParameterType.IsNullable(
            //                underlyingType =>
            //                {
            //                    return onValid(parameterRequiringValidation.ParameterType.GetDefault(), null);
            //                },
            //                () =>
            //                {
            //                    // enum and interfaces cannot be actived
            //                    if (parameterRequiringValidation.ParameterType.IsEnum)
            //                        return onValid(parameterRequiringValidation.ParameterType.GetDefault(), null);
            //                    if (parameterRequiringValidation.ParameterType.IsInterface)
            //                        return onValid(parameterRequiringValidation.ParameterType.GetDefault(), null);

            //                    var instance = Activator.CreateInstance(parameterRequiringValidation.ParameterType);
            //                    return onValid(instance, null);
            //                });
            //    });

        }
    }
}
