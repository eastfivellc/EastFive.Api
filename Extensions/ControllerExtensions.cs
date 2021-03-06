﻿using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Extensions;
using EastFive.Api.Controllers;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using EastFive.Text;
using EastFive;

namespace BlackBarLabs.Api
{
    public static class ControllerExtensions
    {
        public static async Task<TResult> ParseMultipartAsync<T1, TResult>(this HttpContent content,
            Expression<Func<T1, TResult>> callback,
            Func<TResult> onNotMultipart)
        {
            return await ParseMultipartAsync_<Func<T1, TResult>, TResult>(content, callback, onNotMultipart);
        }

        public static async Task<TResult> ParseMultipartAsync<T1, T2, TResult>(this HttpContent content,
            Expression<Func<T1, T2, TResult>> callback,
            Func<TResult> onNotMultipart)
        {
            return await ParseMultipartAsync_<Func<T1, T2, TResult>, TResult>(content, callback, onNotMultipart);
        }

        public static async Task<TResult> ParseMultipartAsync<T1, T2, T3, TResult>(this HttpContent content,
            Expression<Func<T1, T2, T3, TResult>> callback,
            Func<TResult> onNotMultipart)
        {
            return await ParseMultipartAsync_<Func<T1, T2, T3, TResult>, TResult>(content, callback, onNotMultipart);
        }

        public static async Task<TResult> ParseMultipartAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(this HttpContent content,
            Expression<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>> callback,
            Func<TResult> onNotMultipart)
        {
            return await ParseMultipartAsync_<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>, TResult>(content, callback, onNotMultipart);
        }

        public static async Task<TResult> ReadFormDataAsync<TResult>(this HttpContent content,
            Func<System.Collections.Specialized.NameValueCollection, TResult> onFoundFormData,
            Func<TResult> onNotFormData)
        {
            try
            {
                var formData = await content.ReadAsFormDataAsync();
                return onFoundFormData(formData);
            } catch (Exception ex)
            {
                ex.GetType();
                return onNotFormData();
            }
        }
        
        public static Task<TResult> ParseFormDataAsync<TMethod, TResult>(this HttpContent content,
            Expression<TMethod> callback)
        {
            return content.ParseFormDataAsync(callback.Parameters.Select(p => p.Name.PairWithValue(p.Type)),
                paramsForCallback =>
                {
                    var result = ((LambdaExpression)callback).Compile().DynamicInvoke(paramsForCallback);
                    return (TResult)result;
                });
        }

        public static async Task<TResult> ParseFormDataAsync<TResult>(this HttpContent content,
            IEnumerable<KeyValuePair<string, Type>> parameterInfos,
            Func<object[], TResult> onPopulated)
        {
            var formData = await content.ReadAsFormDataAsync();

            var paramsForCallback = parameterInfos
                .Select(
                    (param) =>
                    {
                        var paramContentKey = formData.AllKeys
                            .FirstOrDefault(key => String.Compare(
                                key.Trim(new char[] { '"' }),
                                param.Key,
                                true) == 0);
                        if (default(string) == paramContentKey)
                            return param.Value.IsValueType ? Activator.CreateInstance(param.Value) : null;

                        if (param.Value.GUID == typeof(string).GUID)
                        {
                            var stringValue = formData[paramContentKey];
                            return (object)stringValue;
                        }
                        if (param.Value.GUID == typeof(Guid).GUID)
                        {
                            var guidStringValue = formData[paramContentKey];
                            var guidValue = Guid.Parse(guidStringValue);
                            return (object)guidValue;
                        }
                        if (param.Value.GUID == typeof(System.IO.Stream).GUID)
                        {
                            var streamValue = formData[paramContentKey];
                            return (object)streamValue;
                        }
                        if (param.Value.GUID == typeof(byte[]).GUID)
                        {
                            var byteArrayBase64 = formData[paramContentKey];
                            var byteArrayValue = Convert.FromBase64String(byteArrayBase64);
                            return (object)byteArrayValue;
                        }
                        var value = formData[paramContentKey];
                        return value;
                    }).ToArray();

            return onPopulated(paramsForCallback);
        }
        

        public static async Task<TResult> ReadMultipartContentAsync<TResult>(this HttpContent content,
            Func<IDictionary<string, HttpContent>, TResult> onMultpartContentFound,
            Func<TResult> onNotMultipartContent)
        {
            if (!content.IsMimeMultipartContent())
                return onNotMultipartContent();

            var streamProvider = new MultipartMemoryStreamProvider();
            await content.ReadAsMultipartAsync(streamProvider);

            var contents = streamProvider.Contents
                .Select(file =>
                    file.Headers.ContentDisposition.Name.Trim(new char[] { '"' })
                    .PairWithValue(file))
                .ToDictionary();
            return onMultpartContentFound(contents);
        }

        public static async Task<TResult> ParseMultipartAsync<TResult>(this HttpContent httpContent,
            IEnumerable<KeyValuePair<string, Type>> parameterInfos,
            Func<object [], TResult> onPopulated,
            Func<TResult> onNotMultipart)
        {
            if (httpContent.IsDefaultOrNull())
                return onNotMultipart();

            if (!httpContent.IsMimeMultipartContent())
            {
                if (httpContent.IsFormData())
                    return await httpContent.ParseFormDataAsync(parameterInfos, onPopulated);
                return onNotMultipart();
            }

            var streamProvider = new MultipartMemoryStreamProvider();
            await httpContent.ReadAsMultipartAsync(streamProvider);

            var parametersPopulated = await parameterInfos
                .Select(
                    (param) =>
                    {
                        var paramContent = streamProvider.Contents
                            .FirstOrDefault(file => String.Compare(
                                    file.Headers.ContentDisposition.Name.Trim(new char[] { '"' }),
                                    param.Key,
                                true) == 0);
                        return ContentToTypeAsync(param.Value, paramContent).AsTask();
                    })
                .WhenAllAsync();
            return onPopulated(parametersPopulated);
        }

        private static async Task<object> ContentToTypeAsync(Type type, HttpContent content)
        {
            if (default(HttpContent) == content)
                return type.IsValueType ? Activator.CreateInstance(type) : null;

            if (type.IsAssignableFrom(typeof(string)))
            {
                var stringValue = await content.ReadAsStringAsync();
                return (object)stringValue;
            }
            if (type.IsAssignableFrom(typeof(Guid)))
            {
                var guidStringValue = await content.ReadAsStringAsync();
                var guidValue = Guid.Parse(guidStringValue);
                return (object)guidValue;
            }
            if (type.IsAssignableFrom(typeof(bool)))
            {
                var boolStringValue = await content.ReadAsStringAsync();
                if (bool.TryParse(boolStringValue, out bool boolValue))
                    return (object)boolValue;
                if (boolStringValue.ToLower() == "t")
                    return true;
                if (boolStringValue.ToLower() == "on") // used in check boxes
                    return true;
                return false;
            }
            if (type.IsAssignableFrom(typeof(Stream)))
            {
                var streamValue = await content.ReadAsStreamAsync();
                return (object)streamValue;
            }
            if (type.IsAssignableFrom(typeof(byte[])))
            {
                var byteArrayValue = await content.ReadAsByteArrayAsync();
                return (object)byteArrayValue;
            }
            if (type.IsAssignableFrom(typeof(ByteArrayContent)))
            {
                var byteArrayContentValue = content as ByteArrayContent;
                if (default(ByteArrayContent) == byteArrayContentValue)
                {
                    var byteArrayValue = await content.ReadAsByteArrayAsync();
                    byteArrayContentValue = new ByteArrayContent(byteArrayValue);
                    byteArrayContentValue.Headers.ContentType = content.Headers.ContentType;
                }
                return (object)byteArrayContentValue;
            }
            if (type.IsAssignableFrom(typeof(EastFive.Api.Controllers.ContentBytes)))
            {
                return new EastFive.Api.Controllers.ContentBytes()
                {
                    content = await content.ReadAsByteArrayAsync(),
                    contentType = content.Headers.ContentType,
                };
            }
            if (type.IsAssignableFrom(typeof(EastFive.Api.Controllers.ContentStream)))
            {
                return new EastFive.Api.Controllers.ContentStream()
                {
                    content = await content.ReadAsStreamAsync(),
                    contentType = content.Headers.ContentType,
                };
            }
            var value = await content.ReadAsAsync(type);
            return value;
        }

        public static async Task<TResult> ParseMultipartAsync_<TMethod, TResult>(this HttpContent content,
            Expression<TMethod> callback,
            Func<TResult> onNotMultipart)
        {
            if (content.IsDefaultOrNull())
                return onNotMultipart();

            if (!content.IsMimeMultipartContent())
            {
                if (content.IsFormData())
                    return await content.ParseFormDataAsync<TMethod, TResult>(callback);
                return onNotMultipart();
            }

            var streamProvider = new MultipartMemoryStreamProvider();
            await content.ReadAsMultipartAsync(streamProvider);

            return await content.ParseMultipartAsync(callback.Parameters.Select(p => p.Name.PairWithValue(p.Type)),
                paramsForCallback =>
                {
                    var result = ((LambdaExpression)callback).Compile().DynamicInvoke(paramsForCallback);
                    return (TResult)result;
                },
                onNotMultipart);
        }
        
        public static IHttpActionResult ToActionResult(this HttpActionDelegate action)
        {
            return new HttpActionResult(action);
        }

        public static IHttpActionResult ToActionResult(this HttpResponseMessage response)
        {
            return new HttpActionResult(() => Task.FromResult(response));
        }

        public static IHttpActionResult ToActionResult(this Func<Task<HttpResponseMessage>> executeAsync)
        {
            return new HttpActionResult(() => executeAsync());
        }

        public static IHttpActionResult ActionResult(this System.Web.Http.ApiController controller, HttpActionDelegate action)
        {
            return action.ToActionResult();
        }
    }
}
