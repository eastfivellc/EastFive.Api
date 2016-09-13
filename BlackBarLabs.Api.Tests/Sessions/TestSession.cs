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
using BlackBarLabs.Web;
using Microsoft.WindowsAzure;

namespace BlackBarLabs.Api.Tests
{
    public class TestSession : ITestSession
    {
        public static async Task StartAsync(Func<TestSession, Task> callback)
        {
            var session = new TestSession();
            session.UpdateRequestPropertyFetch(
                BlackBarLabs.Api.ServicePropertyDefinitions.MailService,
                new MockMailService());

            session.UpdateRequestPropertyFetch<BlackBarLabs.Web.Services.ITimeService>(
                BlackBarLabs.Api.ServicePropertyDefinitions.TimeService,
                new TimeService());
            var callbackTask = callback(session);
            await callbackTask;
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
            response.Assert(System.Net.HttpStatusCode.OK);
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

        public T GetRequestPropertyFetch<T>(string propertyKey)
        {
            if (requestPropertyObjects.ContainsKey(propertyKey))
            {
                var currentValue = (T)requestPropertyObjects[propertyKey];
                return currentValue;
            }
            return default(T);
        }
        
        private HttpRequestMessage GetRequest<TController>(TController controller, HttpMethod method)
            where TController : ApiController
        {
            var hostingLocation = CloudConfigurationManager.GetSetting("BlackBarLabs.Api.Tests.ServerUrl");
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
            route = config.Routes.MapHttpRoute(
                name: "Default",
                routeTemplate: "{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
            httpRequest.SetRouteData(new System.Web.Http.Routing.HttpRouteData(route));

            httpRequest.SetConfiguration(config);

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
            if (null == methodInfo)
                Assert.Fail("Method {0} not supported on {1}", methodName, controller.GetType().Name);

            IHttpActionResult resourceFromController;
            if (methodInfo.GetParameters().Length == 2)
            {
                var idProperty = resource.GetType().GetProperty("Id");
                var id = idProperty.GetValue(resource);
                resourceFromController = (IHttpActionResult)methodInfo.Invoke(controller, new object[] { id, resource });
            }
            else if(methodInfo.ReturnType.GUID == typeof(Task<IHttpActionResult>).GUID)
            {
                var resourceFromControllerTask = (Task<IHttpActionResult>)methodInfo.Invoke(controller, new object[] { resource });
                resourceFromController = await resourceFromControllerTask;
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
