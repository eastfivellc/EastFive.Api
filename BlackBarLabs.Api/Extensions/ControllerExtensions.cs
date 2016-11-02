using BlackBarLabs.Api.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Core.Extensions;

namespace BlackBarLabs.Api
{
    public static class ControllerExtensions
    {
        public static async Task<TResult> ParseMultipartAsync<T1, TResult>(this HttpContent content,
            Expression<Func<T1, TResult>> callback)
        {
            return await ParseMultipartAsync_<Func<T1, TResult>, TResult>(content, callback);
        }

        public static async Task<TResult> ParseMultipartAsync<T1, T2, TResult>(this HttpContent content,
            Expression<Func<T1, T2, TResult>> callback)
        {
            return await ParseMultipartAsync_<Func<T1, T2, TResult>, TResult>(content, callback);
        }

        public static async Task<TResult> ParseMultipartAsync<T1, T2, T3, TResult>(this HttpContent content,
            Expression<Func<T1, T2, T3, TResult>> callback)
        {
            return await ParseMultipartAsync_<Func<T1, T2, T3, TResult>, TResult>(content, callback);
        }

        public static async Task<TResult> ParseMultipartAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(this HttpContent content,
            Expression<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>> callback)
        {
            return await ParseMultipartAsync_<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>, TResult>(content, callback);
        }

        public static async Task<TResult> ParseMultipartAsync_<TMethod, TResult>(this HttpContent content,
            Expression<TMethod> callback)
        {
            if (!content.IsMimeMultipartContent())
            {
                throw new ArgumentException("Content is not multipart", "content");
            }

            var streamProvider = new MultipartMemoryStreamProvider();
            await content.ReadAsMultipartAsync(streamProvider);

            var paramTasks = callback.Parameters.Select(
                async (param) =>
                {
                    var paramContent = streamProvider.Contents.FirstOrDefault(file => file.Headers.ContentDisposition.Name.Contains(param.Name));
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
                    if (param.Type.GUID == typeof(byte []).GUID)
                    {
                        var byteArrayValue = await paramContent.ReadAsByteArrayAsync();
                        return (object)byteArrayValue;
                    }
                    var value = await paramContent.ReadAsAsync(param.Type);
                    return value;
                });

            var paramsForCallback = await Task.WhenAll(paramTasks);
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

        public static IHttpActionResult ActionResult(this ApiController controller, HttpActionDelegate action)
        {
            return action.ToActionResult();
        }
    }
}
