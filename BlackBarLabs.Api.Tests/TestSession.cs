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
using BlackBarLabs.Api.Services;

namespace BlackBarLabs.Api.Tests
{
    public class TestSession
    {
        public static async Task StartAsync(Func<TestSession, Task> callback)
        {
            await callback(new TestSession());
        }

        public TestSession()
        {
            Id = Guid.NewGuid();
            Headers = new Dictionary<string, string>();
        }
        public Guid Id { get; set; }
        
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

        public async Task<HttpResponseMessage> GetAsync<TController>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            var controller = GetController<TController>();
            return await InvokeControllerAsync(controller, HttpMethod.Get,
                (request, user) =>
                {
                    return resource;
                });
        }

        public async Task<TResult> GetAsync<TController, TResult>(object resource,
                Action<HttpRequestMessage> mutateRequest = default(Action<HttpRequestMessage>))
            where TController : ApiController
        {
            var response = await this.GetAsync<TController>(resource, mutateRequest);
            var results = response.GetContent<TResult>();
            return results;
        }

        public async Task<TResult> GetAsync<TController, TResult>(object resource,
                HttpActionDelegate<object, TResult> callback)
            where TController : ApiController
        {
            var response = await this.GetAsync<TController>(resource);
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
        }

        #endregion

        private Dictionary<string, object> requestPropertyObjects = new Dictionary<string, object>();
        private Dictionary<string, object> requestPropertyFetches = new Dictionary<string, object>();
        public void UpdateRequestPropertyFetch<T>(string propertyKey, T propertyValue, out T currentValue)
        {
            if (requestPropertyObjects.ContainsKey(propertyKey))
            {
                currentValue = (T)requestPropertyObjects[propertyKey];
                requestPropertyObjects[propertyKey] = propertyValue;
                return;
            }
            currentValue = default(T);
            requestPropertyObjects.Add(propertyKey, propertyValue);

            Func<T> fetchPropertyValue = () => (T)requestPropertyObjects[propertyKey];
            requestPropertyFetches.Add(propertyKey, fetchPropertyValue);
        }
        public void UpdateRequestPropertyFetch<T>(string propertyKey, T propertyValue)
        {
            T discard;
            UpdateRequestPropertyFetch(propertyKey, propertyValue, out discard);
        }

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
            var hostingLocation = System.Configuration.ConfigurationManager.AppSettings["BlackBarLabs.Api.Tests.ServerUrl"];
            if (String.IsNullOrWhiteSpace(hostingLocation))
                hostingLocation = "http://example.com";
            var httpRequest = new HttpRequestMessage(method, hostingLocation);
            var config = new HttpConfiguration();
            var route = config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
            httpRequest.SetRouteData(new System.Web.Http.Routing.HttpRouteData(route));

            httpRequest.SetConfiguration(config);
            httpRequest.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.MailService,
                MailerServiceCreate);

            httpRequest.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.TimeService,
                FetchDateTimeUtc);

            if (default(System.Security.Principal.IPrincipal) != principalUser)
            {
                httpRequest.Properties.Add(
                    BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService,
                    new IdentityService(principalUser.Identity));
                principalUser.UpdateAuthorizationToken();
                controller.User = principalUser;
            }

            foreach(var requestPropertyKvp in requestPropertyFetches)
            {
                httpRequest.Properties.Add(
                    requestPropertyKvp.Key, requestPropertyKvp.Value);
            }

            controller.Request = httpRequest;
            foreach (var headerKVP in Headers)
            {
                httpRequest.Headers.Add(headerKVP.Key, headerKVP.Value);
            }
            
            return httpRequest;
        }

        public Dictionary<string, string> Headers { get; set; }

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

            IHttpActionResult resourceFromController;
            if (methodInfo.GetParameters().Length == 2)
            {
                var idProperty = resource.GetType().GetProperty("Id");
                var id = idProperty.GetValue(resource);
                resourceFromController = (IHttpActionResult)methodInfo.Invoke(controller, new object[] { id, resource });
            }
            else
            {
                resourceFromController = (IHttpActionResult)methodInfo.Invoke(controller, new object[] { resource });
            }
            var response = await resourceFromController.ExecuteAsync(CancellationToken.None);
            foreach (var header in response.Headers)
            {
                if (String.Compare(header.Key, "Set-Cookie", true) == 0)
                {
                    // TODO: Store these for next request
                }
            }
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
            foreach (var header in response.Headers)
            {
                if (String.Compare(header.Key, "Set-Cookie", true) == 0)
                {
                    // TODO: Store these for next request
                }
            }
            return response;
        }
    }
}
