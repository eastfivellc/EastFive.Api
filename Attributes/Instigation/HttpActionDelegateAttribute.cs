﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BlackBarLabs.Extensions;
using EastFive.Api.Modules;
using EastFive.Api.Resources;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace EastFive.Api
{
    public interface IModifyDocumentResponse
    {
        Response GetResponse(Response response, ParameterInfo paramInfo, HttpApplication httpApp);
    }

    public abstract class HttpDelegateAttribute : Attribute, IDocumentResponse, IInstigatable
    {
        public virtual HttpStatusCode StatusCode { get; set; }

        public virtual string Example { get; set; }

        public virtual Task<HttpResponseMessage> Instigate(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
                RequestTelemetry telemetry,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            return InstigateInternal(httpApp, request, parameterInfo,
                    telemetry,
                onSuccess);
            //callback =>
            //{
            //    var callbackType = callback.GetType();
            //    var returnType = (callback as MulticastDelegate).GetMethodInfo().ReturnType;
            //    var callbackArgs = callbackType
            //        .GenericTypeArguments
            //        .Append(returnType)
            //        .ToArray();
            //    var interceptMethodGeneric = this.GetType()
            //        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            //        .Where(methodInfo => methodInfo.Name == "GenerateIntercept")
            //        .Where(methodInfo => methodInfo.GetGenericArguments().Length == callbackArgs.Length)
            //        .Single();
            //    var interceptGenerationMethod = interceptMethodGeneric.MakeGenericMethod(callbackArgs);
            //    var intercept = interceptGenerationMethod.Invoke(this,
            //        new object[] { callback, httpApp, request, parameterInfo, telemetry });
            //    return onSuccess(intercept);
            //});
        }

        public abstract Task<HttpResponseMessage> InstigateInternal(HttpApplication httpApp,
                HttpRequestMessage request, ParameterInfo parameterInfo,
                RequestTelemetry telemetry,
            Func<object, Task<HttpResponseMessage>> onSuccess);

        private void UpdateTelemetry(HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo, 
            RequestTelemetry telemetry)
        {
            telemetry.ResponseCode = this.StatusCode.ToString();
            telemetry.Properties.AddOrReplace(ControllerHandler.TelemetryStatusName, parameterInfo.ParameterType.FullName);
            telemetry.Properties.AddOrReplace(ControllerHandler.TelemetryStatusInstance, parameterInfo.Name);
        }

        public object GenerateIntercept<TResult>(object callback,
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
                RequestTelemetry telemetry)
        {
            var callbackCast = callback as MulticastDelegate;
            Func<TResult> interceptor =
                () =>
                {
                    UpdateTelemetry(httpApp, request, parameterInfo, telemetry);
                    var result = (TResult)callbackCast.DynamicInvoke();
                    return InterceptResult(httpApp, request, parameterInfo, telemetry, result);
                };
            var interceptedCallback = interceptor.MakeDelegate(callback.GetType());
            return interceptedCallback;
        }

        protected virtual TResult InterceptResult<TResult>(
            HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo, 
            RequestTelemetry telemetry, 
            TResult result)
        {
            return result;
        }

        public object GenerateIntercept<T1, TResult>(object callback,
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
                RequestTelemetry telemetry)
        {
            var callbackCast = callback as Func<T1, TResult>;
            Func<T1, TResult> interceptor = (v1) =>
            {
                UpdateTelemetry(httpApp, request, parameterInfo, telemetry);
                var result = (TResult)callbackCast.DynamicInvoke(v1);
                return InterceptResult(httpApp, request, parameterInfo,
                    telemetry,
                    v1, result);
            };
            var interceptedCallback = interceptor.MakeDelegate(callback.GetType());
            return interceptedCallback;
        }

        protected virtual TResult InterceptResult<T1, TResult>(
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
                RequestTelemetry telemetry,
                T1 v1, TResult result)
        {
            return result;
        }

        public object GenerateIntercept<T1, T2, TResult>(object callback,
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
                RequestTelemetry telemetry)
        {
            var callbackCast = callback as Func<T1, T2, TResult>;
            Func<T1, T2, TResult> interceptor = (v1, v2) =>
            {
                UpdateTelemetry(httpApp, request, parameterInfo, telemetry);
                var result = (TResult)callbackCast.DynamicInvoke(v1);
                return InterceptResult(httpApp, request, parameterInfo,
                    telemetry,
                    v1, v2, result);
            };
            var interceptedCallback = interceptor.MakeDelegate(callback.GetType());
            return interceptedCallback;
        }

        protected virtual TResult InterceptResult<T1, T2, TResult>(
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
                RequestTelemetry telemetry,
                T1 v1, T2 v2, TResult result)
        {
            return result;
        }

        public object GenerateIntercept<T1, T2, T3, TResult>(object callback,
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
                RequestTelemetry telemetry)
        {
            var callbackCast = callback as Func<T1, T2, T3, TResult>;
            Func<T1, T2, T3, TResult> interceptor = (v1, v2, v3) =>
            {
                UpdateTelemetry(httpApp, request, parameterInfo, telemetry);
                var result = (TResult)callbackCast.DynamicInvoke(v1);
                return InterceptResult(httpApp, request, parameterInfo,
                    telemetry,
                    v1, v2, v3, result);
            };
            var interceptedCallback = interceptor.MakeDelegate(callback.GetType());
            return interceptedCallback;
        }

        protected virtual TResult InterceptResult<T1, T2, T3, TResult>(
                HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
                RequestTelemetry telemetry,
                T1 v1, T2 v2, T3 v3, TResult result)
        {
            return result;
        }

        public virtual Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var response = new Response()
            {
                Name = paramInfo.Name,
                StatusCode = this.StatusCode,
                Example = this.Example,
                Headers = new KeyValuePair<string, string>[] { },
            };
            return paramInfo
                .GetAttributesInterface<IModifyDocumentResponse>()
                .Aggregate(response,
                    (last, attr) => attr.GetResponse(last, paramInfo, httpApp));
        }
    }

    public abstract class HttpFuncDelegateAttribute : HttpDelegateAttribute
    {
        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var response = base.GetResponse(paramInfo, httpApp);
            if (paramInfo.ParameterType.IsSubClassOfGeneric(typeof(CreatedBodyResponse<>)))
            {
                response.Example = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
                return response;
            }
            if (paramInfo.ParameterType.IsSubClassOfGeneric(typeof(MultipartResponseAsync<>)))
            {
                var typeName = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
                response.Example = $"{typeName}[]";
                return response;
            }
            return response;
        }
    }

    public abstract class HttpHeaderDelegateAttribute : HttpDelegateAttribute
    {
        public string HeaderName { get; set; }
        public string HeaderValue { get; set; }

        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var response = base.GetResponse(paramInfo, httpApp);
            response.Headers = HeaderName.PairWithValue(HeaderValue).AsArray();
            if (paramInfo.ParameterType.IsSubClassOfGeneric(typeof(CreatedBodyResponse<>)))
            {
                response.Example = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
                return response;
            }
            if (paramInfo.ParameterType.IsSubClassOfGeneric(typeof(CreatedBodyResponse<>)))
            {
                response.Example = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
                return response;
            }
            if (paramInfo.ParameterType.IsSubClassOfGeneric(typeof(MultipartResponseAsync<>)))
            {
                var typeName = Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp);
                response.Example = $"{typeName}[]";
                return response;
            }
            return response;
        }
    }
}