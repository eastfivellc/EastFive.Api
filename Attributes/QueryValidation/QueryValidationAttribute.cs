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
using EastFive.Serialization;

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
            var fileResult = await fetchPathParam(parameterRequiringValidation,
                (v) =>
                {
                    return SelectParameterResult.File(v, key, parameterRequiringValidation);
                },
                (whyQuery) => SelectParameterResult.FailureFile(
                    $"Could not create value in file.", 
                    key, parameterRequiringValidation));

            if (fileResult.valid)
                return fileResult;

            var queryResult = await fetchQueryParam(parameterRequiringValidation,
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
                    return SelectParameterResult.FailureQuery(whyQuery, key, parameterRequiringValidation);
                });

            if (queryResult.valid)
                return queryResult;

            return await fetchBodyParam(parameterRequiringValidation,
                (v) =>
                {
                    return SelectParameterResult.Body(v, key, parameterRequiringValidation);
                },
                (whyQuery) =>
                {
                    if (queryResult.valid)
                        return queryResult;
                    return SelectParameterResult.FailureBody(whyQuery, key, parameterRequiringValidation);
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
            var queryResult = await fetchQueryParam(parameterRequiringValidation,
                (v) =>
                {
                    return SelectParameterResult.Query(v, key, parameterRequiringValidation);
                },
                (whyQuery) => SelectParameterResult.FailureQuery(whyQuery, key, parameterRequiringValidation));

            if (queryResult.valid)
                return queryResult;

            if (!CheckFileName)
                return queryResult;

            return await fetchDefaultParam(parameterRequiringValidation,
                (v) =>
                {
                    return SelectParameterResult.File(v, key, parameterRequiringValidation);
                },
                (whyQuery) =>
                {
                    return queryResult;
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

    public class HashedFileAttribute : QueryValidationAttribute, IDocumentParameter
    {
        public override Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
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
            return request.RequestUri
                .VerifyParametersHash(
                    onValid:(id, paramsHash) =>
                    {
                        var resourceType = parameterRequiringValidation
                            .ParameterType.GenericTypeArguments.First();
                        var instantiatableType = typeof(CheckSumRef<>).MakeGenericType(resourceType);
                        var instance = Activator.CreateInstance(instantiatableType, new object[] { id, paramsHash });
                        return SelectParameterResult.File(instance, key, parameterRequiringValidation);
                    },
                    onInvalid:(why) => SelectParameterResult
                        .FailureFile(why, key, parameterRequiringValidation))
                .AsTask();
        }

        public virtual Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Parameter()
            {
                Default = true,
                Name = this.GetKey(paramInfo),
                Required = true,
                Type = $"Hashed({Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp)})",
                Where = "QUERY",
            };
        }
    }

    public class HeaderAttribute : QueryValidationAttribute
    {
        public string Content { get; set; }

        public override Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
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
                    }.AsTask();
                throw new NotImplementedException();
                //return await fetchBodyParam(
                //    new ParameterInfo() { ParameterType = typeof(HttpContent) },
                //    (parts) =>
                //    {
                //        var content = parts as HttpContent;
                //        return new SelectParameterResult
                //        {
                //            valid = true,
                //            fromBody = false,
                //            fromQuery = false,
                //            fromFile = false,
                //            key = Content,
                //            parameterInfo = parameterRequiringValidation,
                //            value = content.Headers.ContentType,
                //        };
                //    },
                //    (why) =>
                //    {
                //        return new SelectParameterResult
                //        {
                //            fromBody = false,
                //            key = "",
                //            fromQuery = false,
                //            fromFile = false,
                //            parameterInfo = parameterRequiringValidation,
                //            valid = false,
                //            failure = why, // $"Cannot extract MediaTypeHeaderValue from non-multipart request.",
                //        };
                //    });
            }
            if (bindType.IsSubClassOfGeneric(typeof(HttpHeaderValueCollection<>)))
            {
                if (bindType.GenericTypeArguments.First() == typeof(StringWithQualityHeaderValue))
                {
                    if (Content.HasBlackSpace())
                        return new SelectParameterResult
                        {
                            key = "",
                            fromQuery = false,
                            fromBody = false,
                            fromFile = false,
                            parameterInfo = parameterRequiringValidation,
                            valid = false,
                            failure = "AcceptLanguage is not a content header.",
                        }.AsTask();
                    return new SelectParameterResult
                    {
                        valid = true,
                        fromBody = false,
                        fromQuery = false,
                        fromFile = false,
                        key = default,
                        parameterInfo = parameterRequiringValidation,
                        value = request.Headers.AcceptLanguage,
                    }.AsTask();
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
            }.AsTask();
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
        public override Task<SelectParameterResult> TryCastAsync(IApplication httpApp,
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
            }.AsTask();
        }
    }

    
}
