using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace BlackBarLabs.Api.Tests
{
    public class TestSession
    {
        public async static Task StartAsync(Func<TestSession, Task> callback)
        {
            await callback(new TestSession());
        }
        
        #region Methods

        public async Task<HttpResponseMessage> PostAsync<TController>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            var controller = GetController<TController>();
            return await InvokeControllerAsync(controller, HttpMethod.Post,
                (request, user) =>
                {
                    return resource;
                });
        }
        
        public async Task<HttpResponseMessage> PostMultipartAsync<TController>(Action<MultipartContent> multipartContentCallback)
            where TController : ApiController
        {
            var controller = GetController<TController>();
            var response = await InvokeControllerAsync(controller, HttpMethod.Post,
                (httpRequest, user) =>
                {
                    var multipartContent = new MultipartContent();
                    multipartContentCallback(multipartContent);
                    httpRequest.Content = multipartContent;
                });
            return response;
        }
        
        public async Task<HttpResponseMessage> PutAsync<TController>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            var controller = GetController<TController>();
            return await InvokeControllerAsync(controller, HttpMethod.Put,
                (request, user) =>
                {
                    return resource;
                });
        }

        public async Task<HttpResponseMessage> PutMultipartAsync<TController>(Action<MultipartContent> multipartContentCallback)
            where TController : ApiController
        {
            var controller = GetController<TController>();
            var response = await InvokeControllerAsync(controller, HttpMethod.Put,
                (httpRequest, user) =>
                {
                    var multipartContent = new MultipartContent();
                    multipartContentCallback(multipartContent);
                    httpRequest.Content = multipartContent;
                });
            return response;
        }

        public async Task<TResult> GetAsync<TController, TResult>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            var controller = GetController<TController>();
            var response = await InvokeControllerAsync(controller, HttpMethod.Get,
                (request, user) =>
                {
                    return resource;
                });
            var results = response.GetContent<TResult>();
            return results;
        }

        public async Task<TResult> GetAsync<TController, TResult>(object resource,
                HttpActionDelegate<object, TResult> callback)
            where TController : ApiController
        {
            var controller = GetController<TController>();
            var response = await InvokeControllerAsync(controller, HttpMethod.Get,
                (request, user) =>
                {
                    return resource;
                });
            var results = callback(response, resource);
            return results;
        }

        public async Task<HttpResponseMessage> DeleteAsync<TController>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            var controller = GetController<TController>();
            return await InvokeControllerAsync(controller, HttpMethod.Delete,
                (request, user) =>
                {
                    return resource;
                });
        }

        #endregion

        #region Request Modifiers

        public async Task WithUserAsync(Guid userId, Func<TestUser, Task> callback)
        {
            this.principalUser = new TestUser(this, userId);
            await callback(this.principalUser);

            ////Get the Auth Token
            //var tokenUrl = "http://hgorderowltest.azurewebsites.net/token";
            //var userName = "test@test.com";
            //var userPassword = "Testing0wer93@";
            //var request = string.Format("grant_type=password&username={0}&password={1}", HttpUtility.UrlEncode(userName), HttpUtility.UrlEncode(userPassword));
            //var tokenMeta = JObject.Parse(HttpPost(tokenUrl, request));
            //var accessToken = tokenMeta["access_token"].ToString();//.ToObject<string>();

            //Add the token
            // httpRequest.Headers.Add("Authorization", "Bearer " + ""); //add this to the "" accessToken
        }

        #endregion

        private MockMailService.SendEmailMessageDelegate sendMessageCallback;
        public MockMailService.SendEmailMessageDelegate SendMessageCallback
        {
            get
            {
                if(default(MockMailService.SendEmailMessageDelegate) == sendMessageCallback)
                    return 
                        async (toAddress, fromAddress, fromName,
                            subject, html, substitution) =>
                        {
                            await Task.FromResult(true);
                        };
                return sendMessageCallback;
            }
            set
            {
                sendMessageCallback = value;
            }
        }

        private Func<Web.ISendMailService> mailerServiceCreate;
        public Func<Web.ISendMailService> MailerServiceCreate
        {
            get
            {
                if (default(Func<Web.ISendMailService>) != mailerServiceCreate)
                    return mailerServiceCreate;

                return () =>
                {
                    var mockMailService = new MockMailService();
                    mockMailService.SendEmailMessageCallback = this.SendMessageCallback;
                    return mockMailService;
                };
            }
            set
            {
                mailerServiceCreate = value;
            }
        }

        private Func<DateTime> fetchDateTimeUtc;
        public Func<DateTime> FetchDateTimeUtc
        {
            get
            {
                if (default(Func<DateTime>) != fetchDateTimeUtc)
                    return fetchDateTimeUtc;

                return () => DateTime.UtcNow;
            }
            set
            {
                fetchDateTimeUtc = value;
            }
        }

        private TestUser principalUser = default(TestUser);
        
        private HttpRequestMessage GetRequest<TController>(TController controller, HttpMethod method)
            where TController : ApiController
        {
            var httpRequest = new HttpRequestMessage(method, "http://example.com");
            httpRequest.SetConfiguration(new HttpConfiguration());
            httpRequest.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.MailService,
                MailerServiceCreate);

            httpRequest.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.TimeService,
                FetchDateTimeUtc);

            controller.Request = httpRequest;
            if (default(System.Security.Principal.IPrincipal) != principalUser)
                controller.User = principalUser;

            return httpRequest;
        }

        private TController GetController<TController>()
            where TController : ApiController
        {
            var controller = Activator.CreateInstance<TController>();
            return controller;
        }

        private delegate T InvokeControllerDelegate<T>(HttpRequestMessage request, MockPrincipal user);

        private async Task<HttpResponseMessage> InvokeControllerAsync<TController>(
                TController controller,
                HttpMethod method,
                InvokeControllerDelegate<object> callback)
            where TController : ApiController
        {
            var httpRequest = GetRequest(controller, method);
            var resource = callback(httpRequest, controller.User as MockPrincipal);

            var methodName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(method.ToString().ToLower());
            var methodInfo = typeof(TController).GetMethod(methodName);

            if (methodInfo.GetParameters().Length == 2)
            {
                var idProperty = resource.GetType().GetProperty("Id");
                var id = idProperty.GetValue(resource);
                var putResult = (IHttpActionResult)methodInfo.Invoke(controller, new object[] { id, resource });
                var putResponse = await putResult.ExecuteAsync(CancellationToken.None);
                return putResponse;
            }
            var resourceFromController = (IHttpActionResult)methodInfo.Invoke(controller, new object[] { resource });
            var response = await resourceFromController.ExecuteAsync(CancellationToken.None);
            return response;
        }

        private delegate void InvokeControllerDelegate(HttpRequestMessage request, MockPrincipal user);
        private async Task<HttpResponseMessage> InvokeControllerAsync<TController>(
                TController controller,
                HttpMethod method,
                InvokeControllerDelegate callback)
            where TController : ApiController
        {
            var httpRequest = GetRequest(controller, method);

            callback(httpRequest, controller.User as MockPrincipal);

            var methodName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(method.ToString().ToLower());
            var methodInfo = typeof(TController).GetMethod(methodName);

            if (methodInfo.GetParameters().Length != 0)
                Assert.Fail("Wrong InvokeControllerAsync method called, this one is for parameterless methods");

            var resultTask = (Task<IHttpActionResult>)methodInfo.Invoke(controller, new object[] {});
            var result = await resultTask;
            var response = await result.ExecuteAsync(CancellationToken.None);
            return response;
        }
    }
}
