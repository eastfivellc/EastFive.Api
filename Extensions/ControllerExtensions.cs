using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

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

        public static async Task<TResult> ParseFormDataAsync<TMethod, TResult>(this HttpContent content,
            Expression<TMethod> callback)
        {
            var formData = await content.ReadAsFormDataAsync();

            var paramsForCallback = callback.Parameters
                .Select(
                (param) =>
                {
                    var paramContentKey = formData.AllKeys
                        .FirstOrDefault(key => String.Compare(
                                key.Trim(new char[] { '"' }),
                                param.Name,
                            true) == 0);
                    if (default(string) == paramContentKey)
                        return param.Type.IsValueType ? Activator.CreateInstance(param.Type) : null;

                    if (param.Type.GUID == typeof(string).GUID)
                    {
                        var stringValue = formData[paramContentKey];
                        return (object)stringValue;
                    }
                    if (param.Type.GUID == typeof(Guid).GUID)
                    {
                        var guidStringValue = formData[paramContentKey];
                        var guidValue = Guid.Parse(guidStringValue);
                        return (object)guidValue;
                    }
                    if (param.Type.GUID == typeof(System.IO.Stream).GUID)
                    {
                        var streamValue = formData[paramContentKey];
                        return (object)streamValue;
                    }
                    if (param.Type.GUID == typeof(byte[]).GUID)
                    {
                        var byteArrayBase64 = formData[paramContentKey];
                        var byteArrayValue = Convert.FromBase64String(byteArrayBase64);
                        return (object)byteArrayValue;
                    }
                    var value = formData[paramContentKey];
                    return value;
                }).ToArray();

            var result = ((LambdaExpression)callback).Compile().DynamicInvoke(paramsForCallback);
            return (TResult)result;
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

        public static async Task<TResult> ParseMultipartAsync_<TMethod, TResult>(this HttpContent content,
            Expression<TMethod> callback,
            Func<TResult> onNotMultipart)
        {
            if (!content.IsMimeMultipartContent())
            {
                if (content.IsFormData())
                    return await content.ParseFormDataAsync<TMethod, TResult>(callback);
                return onNotMultipart();
            }

            var streamProvider = new MultipartMemoryStreamProvider();
            await content.ReadAsMultipartAsync(streamProvider);

            var paramsForCallback = await callback.Parameters
                .Select(
                async (param) =>
                {
                    var paramContent = streamProvider.Contents
                        .FirstOrDefault(file => String.Compare(
                                file.Headers.ContentDisposition.Name.Trim(new char[] { '"' }),
                                param.Name,
                            true) == 0);
                    if (default(HttpContent) == paramContent)
                        return param.Type.IsValueType ? Activator.CreateInstance(param.Type) : null;

                    if (param.Type.GUID == typeof(string).GUID)
                    {
                        var stringValue = await paramContent.ReadAsStringAsync();
                        return (object)stringValue;
                    }
                    if (param.Type.GUID == typeof(Guid).GUID)
                    {
                        var guidStringValue = await paramContent.ReadAsStringAsync();
                        var guidValue = Guid.Parse(guidStringValue);
                        return (object)guidValue;
                    }
                    if (param.Type.GUID == typeof(System.IO.Stream).GUID)
                    {
                        var streamValue = await paramContent.ReadAsStreamAsync();
                        return (object)streamValue;
                    }
                    if (param.Type.GUID == typeof(byte[]).GUID)
                    {
                        var byteArrayValue = await paramContent.ReadAsByteArrayAsync();
                        return (object)byteArrayValue;
                    }
                    if (param.Type.GUID == typeof(ByteArrayContent).GUID)
                    {
                        var byteArrayContentValue = paramContent as ByteArrayContent;
                        if (default(ByteArrayContent) == byteArrayContentValue)
                        {
                            var byteArrayValue = await paramContent.ReadAsByteArrayAsync();
                            byteArrayContentValue = new ByteArrayContent(byteArrayValue);
                            byteArrayContentValue.Headers.ContentType = paramContent.Headers.ContentType;
                        }
                        return (object)byteArrayContentValue;
                    }
                    var value = await paramContent.ReadAsAsync(param.Type);
                    return value;
                })
                .WhenAllAsync();
            
            var result = ((LambdaExpression)callback).Compile().DynamicInvoke(paramsForCallback);
            return (TResult)result;
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

        public static IHttpActionResult ActionResult(this ApiController controller, HttpActionDelegate action)
        {
            return action.ToActionResult();
        }
    }
}
