using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace BlackBarLabs.Api.Tests
{
    public interface ITestSession
    {
        Task<HttpResponseMessage> GetAsync<TController>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController;

        Task<TResult> GetAsync<TController, TResult>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController;

        Task<TResult> GetAsync<TController, TResult>(object resource,
                HttpActionDelegate<object, TResult> callback)
            where TController : ApiController;

        Task<HttpResponseMessage> PostAsync<TController>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController;

        Task<HttpResponseMessage> PostMultipartAsync<TController>(Action<MultipartContent> multipartContentCallback)
            where TController : ApiController;

        Task<HttpResponseMessage> PutAsync<TController>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController;

        Task<HttpResponseMessage> PutMultipartAsync<TController>(Action<MultipartContent> multipartContentCallback)
            where TController : ApiController;

        Task<HttpResponseMessage> DeleteAsync<TController>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController;

        Task<TResult> OptionsAsync<TController, TResult>(
                Func<HttpResponseMessage, HttpMethod[], TResult> callback)
            where TController : ApiController;

        Dictionary<string, string> Headers { get; set; }

        T GetRequestPropertyFetch<T>(string propertyKey);

        void UpdateRequestPropertyFetch<T>(string propertyKey, T propertyValue, out T currentValue);
    }
}
