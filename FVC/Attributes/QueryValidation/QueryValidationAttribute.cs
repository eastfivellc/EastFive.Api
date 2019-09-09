using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Api.Resources;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class QueryValidationAttribute : System.Attribute, IBindApiValue
    {
        public virtual string Name { get; set; }

        public virtual async Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
                HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
                Api.CastDelegate<SelectParameterResult> fetchQueryParam,
                Api.CastDelegate<SelectParameterResult> fetchBodyParam,
                Api.CastDelegate<SelectParameterResult> fetchPathParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
        {
            var key = GetKey(parameterRequiringValidation);
            var fileResult = await fetchPathParam(key, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    return SelectParameterResult.File(v, key, parameterRequiringValidation);
                },
                (whyQuery) => SelectParameterResult.Failure($"Could not create value in query, body, or file.", key, parameterRequiringValidation));
            //if(fileResult.valid)
            //    if (!matchAllQueryParameters)
            //        if (!matchAllBodyParameters)
            //            return fileResult;
            if (fileResult.valid)
                return fileResult;

            var queryResult = await fetchQueryParam(key.ToLower(),
                    parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    if(fileResult.valid)
                    {
                        fileResult.fromQuery = true;
                        return fileResult;
                    }
                    return SelectParameterResult.Query(v, key, parameterRequiringValidation);
                },
                (whyQuery) =>
                {
                    if (fileResult.valid)
                        return fileResult;
                    return SelectParameterResult.Failure(whyQuery, key, parameterRequiringValidation);
                });
            //if (queryResult.valid)
            //    if (!matchAllBodyParameters)
            //        return queryResult;
            if (queryResult.valid)
                return queryResult;

            return await fetchBodyParam(key, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    if (queryResult.valid)
                    {
                        queryResult.fromBody = true;
                        return queryResult;
                    }
                    return SelectParameterResult.Body(v, key, parameterRequiringValidation);
                },
                (whyQuery) =>
                {
                    if (queryResult.valid)
                        return queryResult;
                    return SelectParameterResult.Failure(whyQuery, key, parameterRequiringValidation);
                });
        }

        public virtual string GetKey(ParameterInfo paramInfo)
        {
            if (this.Name.HasBlackSpace())
                return this.Name;
            return paramInfo.Name;
        }
        
    }

    public class QueryParameterAttribute : QueryValidationAttribute, IDocumentParameter
    {
        public bool CheckFileName { get; set; }

        public override async Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
                HttpRequestMessage request, MethodInfo method,
                ParameterInfo parameterRequiringValidation,
                CastDelegate<SelectParameterResult> fetchQueryParam,
                CastDelegate<SelectParameterResult> fetchBodyParam,
                CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
        {
            var key = this.GetKey(parameterRequiringValidation);
            var queryResult = await fetchQueryParam(key, parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    return SelectParameterResult.Query(v, key, parameterRequiringValidation);
                },
                (whyQuery) => SelectParameterResult.Failure(whyQuery, key, parameterRequiringValidation));

            if (queryResult.valid)
                return queryResult;

            if (!CheckFileName)
                return queryResult;

            return await fetchDefaultParam(key,
                    parameterRequiringValidation.ParameterType,
                (v) =>
                {
                    return SelectParameterResult.File(v, key, parameterRequiringValidation);
                },
                (whyQuery) =>
                {
                    return SelectParameterResult.Failure(whyQuery, key, parameterRequiringValidation);
                });
        }

        public override string GetKey(ParameterInfo paramInfo)
        {
            return base.GetKey(paramInfo).ToLower();
        }

        public virtual Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Parameter()
            {
                Default = CheckFileName,
                Name = this.GetKey(paramInfo),
                Required = true,
                Type = Parameter.GetTypeName(paramInfo.ParameterType, httpApp),
                Where = "QUERY",
            };
        }
    }

    public class OptionalQueryParameterAttribute : QueryParameterAttribute
    {
        public async override Task<SelectParameterResult> TryCastAsync(IApplication httpApp, 
                HttpRequestMessage request, MethodInfo method,
                ParameterInfo parameterRequiringValidation, 
                CastDelegate<SelectParameterResult> fetchQueryParam, 
                CastDelegate<SelectParameterResult> fetchBodyParam, 
                CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
        {
            var baseValue = await base.TryCastAsync(httpApp, request, method,
                parameterRequiringValidation,
                fetchQueryParam, fetchBodyParam, fetchDefaultParam,
                matchAllPathParameters, matchAllQueryParameters, matchAllBodyParameters);
            if (baseValue.valid)
                return baseValue;

            baseValue.value = GetValue();
            baseValue.valid = true;
            baseValue.fromQuery = true;
            return baseValue;
            object GetValue()
            {
                if (parameterRequiringValidation.ParameterType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                {
                    var instanceType = typeof(RefOptional<>).MakeGenericType(
                        parameterRequiringValidation.ParameterType.GenericTypeArguments);
                    return Activator.CreateInstance(instanceType, new object[] { });
                }

                return parameterRequiringValidation.ParameterType.GetDefault();
            }
        }

        public override Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var parameter = base.GetParameter(paramInfo, httpApp);
            parameter.Required = false;
            return parameter;
        }
    }

    public class QueryIdAttribute : QueryParameterAttribute
    {
        public override string Name
        {
            get
            {
                var name = base.Name;
                if (name.HasBlackSpace())
                    return name;
                return "id";
            }
            set => base.Name = value;
        }

        public async override Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
                HttpRequestMessage request, MethodInfo method,
                ParameterInfo parameterRequiringValidation,
                CastDelegate<SelectParameterResult> fetchQueryParam,
                CastDelegate<SelectParameterResult> fetchBodyParam,
                CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
        {
            base.CheckFileName = true;
            return await base.TryCastAsync(httpApp, request, method,
                parameterRequiringValidation, fetchQueryParam, fetchBodyParam, fetchDefaultParam,
                matchAllPathParameters, matchAllQueryParameters, matchAllBodyParameters);
        }

        public override Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var parameter = base.GetParameter(paramInfo, httpApp);
            parameter.Default = true;
            return parameter;
        }
    }

    public class UpdateIdAttribute : QueryValidationAttribute, IDocumentParameter
    {
        public override string Name
        {
            get
            {
                var name = base.Name;
                if (name.HasBlackSpace())
                    return name;
                return "id";
            }
            set => base.Name = value;
        }

        public Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Parameter()
            {
                Default = true,
                Name = this.GetKey(paramInfo),
                Required = true,
                Type = Parameter.GetTypeName(paramInfo.ParameterType, httpApp),
                Where = "QUERY|BODY",
            };
        }
    }

    public class HeaderAttribute : QueryValidationAttribute
    {
        public string Content { get; set; }

        public override async Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
            HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate<SelectParameterResult> fetchQueryParam,
            CastDelegate<SelectParameterResult> fetchBodyParam,
            CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
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
                        fromFile = false,
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
                            fromFile = false,
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
                            fromFile = false,
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
                            key = "",
                            fromQuery = false,
                            fromBody = false,
                            fromFile = false,
                            parameterInfo = parameterRequiringValidation,
                            valid = false,
                            failure = "AcceptLanguage is not a content header.",
                        };
                    return new SelectParameterResult
                    {
                        valid = true,
                        fromBody = false,
                        fromQuery = false,
                        fromFile = false,
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

    public class AcceptsAttribute : Attribute, IBindApiValue
    {
        public string Media { get; set; }

        public string GetKey(ParameterInfo paramInfo)
        {
            return "__accept-header__";
        }

        public async Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
            HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate<SelectParameterResult> fetchQueryParam,
            CastDelegate<SelectParameterResult> fetchBodyParam,
            CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
        {
            var bindType = parameterRequiringValidation.ParameterType;
            if (typeof(MediaTypeWithQualityHeaderValue) == bindType)
            {
                if (request.Headers.IsDefaultOrNull())
                    return new SelectParameterResult
                    {
                        valid = false,
                        fromBody = false,
                        fromQuery = false,
                        fromFile = false,
                        key = "",
                        parameterInfo = parameterRequiringValidation,
                        failure = "No headers sent with request.",
                    };
                if (request.Headers.Accept.IsDefaultOrNull())
                    return new SelectParameterResult
                    {
                        valid = true,
                        fromBody = false,
                        fromQuery = false,
                        fromFile = false,
                        key = GetKey(parameterRequiringValidation),
                        parameterInfo = parameterRequiringValidation,
                        value = default(MediaTypeWithQualityHeaderValue),
                    };
                return request.Headers.Accept
                    .Where(accept => accept.MediaType.ToLower().Contains(this.Media))
                    .First(
                        (accept, next) =>
                        {
                            return new SelectParameterResult
                            {
                                valid = true,
                                fromBody = false,
                                fromQuery = false,
                                fromFile = false,
                                key = GetKey(parameterRequiringValidation),
                                parameterInfo = parameterRequiringValidation,
                                value = accept,
                            };
                        },
                        () =>
                        {
                            return new SelectParameterResult
                            {
                                valid = true,
                                fromBody = false,
                                fromQuery = false,
                                fromFile = false,
                                key = GetKey(parameterRequiringValidation),
                                parameterInfo = parameterRequiringValidation,
                                value = default(MediaTypeWithQualityHeaderValue),
                            };
                        });
            }
            return new SelectParameterResult
            {
                key = "",
                fromQuery = false,
                fromFile = false,
                fromBody = false,
                parameterInfo = parameterRequiringValidation,
                valid = false,
                failure = $"No accept binding for type `{bindType.FullName}` (try MediaTypeWithQualityHeaderValue).",
            };
        }
    }

    public class HeaderLogAttribute : QueryValidationAttribute
    {
        public override async Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
            HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
            CastDelegate<SelectParameterResult> fetchQueryParam,
            CastDelegate<SelectParameterResult> fetchBodyParam,
            CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
        {
            var logger = httpApp.Logger;
            return new SelectParameterResult
            {
                value = logger,
                key = "",
                fromBody = false,
                fromQuery = false,
                fromFile = false,
                parameterInfo = parameterRequiringValidation,
                valid = true,
            };
        }
    }

    public class PropertyAttribute : QueryValidationAttribute, IDocumentParameter
    {
        public override Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
                HttpRequestMessage request, MethodInfo method, ParameterInfo parameterRequiringValidation,
                CastDelegate<SelectParameterResult> fetchQueryParam,
                CastDelegate<SelectParameterResult> fetchBodyParam,
                CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
        {
            var name = this.GetKey(parameterRequiringValidation);
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

        public virtual Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Parameter()
            {
                Default = false,
                Name = this.GetKey(paramInfo),
                Required = true,
                Type = Parameter.GetTypeName(paramInfo.ParameterType, httpApp),
                Where = "BODY",
            };
        }
    }

    public class PropertyOptionalAttribute : PropertyAttribute
    {
        public async override Task<SelectParameterResult> TryCastAsync(
                IApplication httpApp, HttpRequestMessage request, MethodInfo method,
                ParameterInfo parameterRequiringValidation,
                CastDelegate<SelectParameterResult> fetchQueryParam, 
                CastDelegate<SelectParameterResult> fetchBodyParam, 
                CastDelegate<SelectParameterResult> fetchDefaultParam,
            bool matchAllPathParameters,
            bool matchAllQueryParameters,
            bool matchAllBodyParameters)
        {
            var baseValue = await base.TryCastAsync(httpApp, request, method,
                parameterRequiringValidation, fetchQueryParam, fetchBodyParam, fetchDefaultParam,
                matchAllPathParameters, matchAllQueryParameters, matchAllBodyParameters);
            if (baseValue.valid)
                return baseValue;

            baseValue.valid = true;
            baseValue.fromBody = true;
            baseValue.value = GetValue();
            return baseValue;

            object GetValue()
            {
                var parameterType = parameterRequiringValidation.ParameterType;
                if (parameterType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                {
                    var refType = parameterType.GenericTypeArguments.First();
                    var parameterTypeGeneric = typeof(RefOptional<>).MakeGenericType(new Type[] { refType });
                    return Activator.CreateInstance(parameterTypeGeneric, new object[] { });
                }

                if (parameterType.IsSubClassOfGeneric(typeof(IRefs<>)))
                {
                    var refType = parameterType.GenericTypeArguments.First();
                    var parameterTypeGeneric = typeof(Refs<>).MakeGenericType(new Type[] { refType });
                    var refIds = new Guid[] { };
                    var refIdsLookupType = typeof(Func<,>).MakeGenericType(new Type[] { typeof(Guid), refType });
                    var refIdsLookup = refIdsLookupType.GetDefault();
                    return Activator.CreateInstance(
                        parameterTypeGeneric, new object[] { refIds });
                }

                if (parameterType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
                {
                    var parameterTypeGeneric = typeof(Dictionary<,>).MakeGenericType(parameterType.GenericTypeArguments);
                    return Activator.CreateInstance(parameterTypeGeneric);
                }

                return parameterType.GetDefault();
            }
        }

        public override Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var parameter = base.GetParameter(paramInfo, httpApp);
            parameter.Required = false;
            return parameter;
        }
    }
}
