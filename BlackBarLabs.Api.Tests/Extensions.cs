using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace BlackBarLabs.Api.Tests
{
    public static class Extensions
    {
        private static async Task<HttpResponseMessage> Action<TController, TResource>(
                this TResource resource,
                HttpMethod method,
                string userId = default(string),
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            var controller = Activator.CreateInstance<TController>();

            var httpRequest = new HttpRequestMessage(method, "http://example.com");
            httpRequest.SetConfiguration(new HttpConfiguration());

            #region Mock Services

            #region Mailer

            Func<BlackBarLabs.Web.ISendMailService> mailerServiceCreate =
                () =>
                {
                    var mockMailService = new MockMailService();
                    mockMailService.SendEmailMessageCallback =
                        async (toAddress, fromAddress, fromName,
                        subject, html, substitution) =>
                        {
                            await Task.FromResult(true);
                        };
                    return mockMailService;
                };

            httpRequest.Properties.Add(
                ServicePropertyDefinitions.MailService,
                mailerServiceCreate);

            #endregion

            #region Time

            Func<DateTime> fetchDateTimeUtc =
                () => DateTime.UtcNow;
            httpRequest.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.TimeService,
                fetchDateTimeUtc);

            #endregion

            #endregion

            if (default(Action<HttpRequestMessage>) != mutateRequest)
                mutateRequest(httpRequest);
            controller.Request = httpRequest;
            controller.User = new MockPrincipal(userId);
            
            var methodName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(method.ToString().ToLower());
            var methodInfo = typeof(TController).GetMethod(methodName);

            if (methodInfo.GetParameters().Length == 2)
            {
                var idProperty = typeof(TResource).GetProperty("Id");
                var id = idProperty.GetValue(resource);
                var putResult = (IHttpActionResult)methodInfo.Invoke(controller, new object[] { id, resource });
                var putResponse = await putResult.ExecuteAsync(CancellationToken.None);
                return putResponse;
            }
            var result = (IHttpActionResult)methodInfo.Invoke(controller, new object[] { resource });
            var response = await result.ExecuteAsync(CancellationToken.None);
            return response;
        }

        public static async Task<HttpResponseMessage> Post<TController, TResource>(this TResource resource,
                string userId = default(string),
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            return await resource.Action<TController, TResource>(HttpMethod.Post, userId, mutateRequest);
        }

        public static async Task<HttpResponseMessage> Put<TController, TResource>(this TResource resource, string userId = default(string),
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            return await resource.Action<TController, TResource>(HttpMethod.Put, userId, mutateRequest);
        }

        public static async Task<HttpResponseMessage> Delete<TController, TResource>(this TResource resource, string userId = default(string),
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            return await resource.Action<TController, TResource>(HttpMethod.Delete, userId, mutateRequest);
        }

        public static async Task<HttpResponseMessage> Get<TController, TResource>(this TResource resource, string userId = default(string),
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            return await resource.Action<TController, TResource>(HttpMethod.Get, userId, mutateRequest);
        }

        public static async Task<TResult> Get<TController, TResource, TResult>(this TResource resource, string userId = default(string),
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            var response = await resource.Get<TController, TResource>(userId, mutateRequest);
            var content = response.Content as System.Net.Http.ObjectContent<TResult>;
            if (default(ObjectContent<TResult>) == content)
            {
                throw new Exception(
                    String.Format("Expected System.Net.Http.ObjectContent<{0}> but got type {1} in get",
                        typeof(TResult).FullName, response.Content.GetType().FullName));
            }
            var results = (TResult)content.Value;
            return results;
        }
    }
}
