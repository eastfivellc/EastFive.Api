using EastFive.Collections.Generic;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace EastFive.Api
{
    public class InvokeApplicationRemote : InvokeApplication
    {
        public InvokeApplicationRemote(Uri serverUrl, string apiRouteName) : base(serverUrl, apiRouteName)
        {
        }

        public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequest)
        {
            using (var client = new HttpClient())
            {
                var response = await client.SendAsync(httpRequest);

                if (instigators.ContainsKey(response.StatusCode))
                {
                    var instigator = instigators[response.StatusCode];
                    var resultAttempt = await instigator(null, httpRequest, null,
                        async (data) =>
                        {
                            var dataType = data.GetType();
                            var invokeMethod = dataType.GetMethod("Invoke");
                            var invokeParameters = invokeMethod.GetParameters();
                            if (!invokeParameters.Any())
                            {
                                var invokeResponseObj = (data as Delegate).DynamicInvoke(new object[] { });
                                var invokeResponse = invokeResponseObj as HttpResponseMessage;
                                return invokeResponse;
                            }
                            return await response.AsTask();
                        });
                    if (resultAttempt is InvokeApplicationExtensions.IReturnResult)
                    {
                        return resultAttempt;
                    }
                }

                if (instigatorsGeneric.ContainsKey(response.StatusCode))
                {
                    var instigatorGeneric = instigatorsGeneric[response.StatusCode];
                    return await instigatorGeneric(default(Type), null, httpRequest, null,
                        async (data) =>
                        {
                            var dataType = data.GetType();
                            if (dataType.IsSubClassOfGeneric(typeof(CreatedBodyResponse<>)))
                            {
                                var jsonString = await response.Content.ReadAsStringAsync();
                                var resourceType = dataType.GenericTypeArguments.First();
                                var converter = new Serialization.Converter();
                                var instance = Newtonsoft.Json.JsonConvert.DeserializeObject(
                                    jsonString, resourceType, converter);
                                var responseDelegate = ((Delegate)data).DynamicInvoke(
                                    instance, response.Content.Headers.ContentType.MediaType);
                                return (HttpResponseMessage)responseDelegate;
                            }
                            if (dataType.IsSubClassOfGeneric(typeof(ExecuteBackgroundResponseAsync)))
                            {
                                //var jsonString = await response.Headers.();
                                //var resourceType = dataType.GenericTypeArguments.First();
                                //var converter = new RefConverter();
                                //var instance = Newtonsoft.Json.JsonConvert.DeserializeObject(
                                //    jsonString, resourceType, converter);
                                var responseDelegate = ((Delegate)data).DynamicInvoke(
                                    new ExecuteContext());
                                return (HttpResponseMessage)responseDelegate;
                            }
                            return response;
                        });
                }

                return response;
            }
        }


        private Dictionary<HttpStatusCode, InstigatorDelegate> instigators =
            new Dictionary<HttpStatusCode, InstigatorDelegate>();

        public void SetInstigator(Type type, InstigatorDelegate instigator, bool clear = false)
        {
            if (type.ContainsCustomAttribute<HttpDelegateAttribute>())
            {
                var actionDelAttr = type.GetCustomAttribute<HttpDelegateAttribute>();
                var code = actionDelAttr.StatusCode;

                if (!clear)
                {
                    instigators.AddOrReplace(code, instigator);
                    return;
                }
                if (instigators.ContainsKey(code))
                    instigators.Remove(code);

            }
        }

        private Dictionary<HttpStatusCode, InstigatorDelegateGeneric> instigatorsGeneric =
            new Dictionary<HttpStatusCode, InstigatorDelegateGeneric>();

        public override IApplication Application => new HttpApplication();

        public void SetInstigatorGeneric(Type type, InstigatorDelegateGeneric instigator,
            bool clear = false)
        {
            if (type.ContainsCustomAttribute<HttpDelegateAttribute>())
            {
                var actionDelAttr = type.GetCustomAttribute<HttpDelegateAttribute>();
                var code = actionDelAttr.StatusCode;
                if (!clear)
                {
                    instigatorsGeneric.AddOrReplace(code, instigator);
                    return;
                }
                if (instigatorsGeneric.ContainsKey(code))
                    instigatorsGeneric.Remove(code);
            }
        }

        protected override RequestMessage<TResource> BuildRequest<TResource>(IApplication application, HttpRequestMessage httpRequest)
        {
            return new RequestMessage<TResource>(this, httpRequest);
        }

        public class ExecuteContext : IExecuteAsync
        {
            public bool ForceBackground => false;

            public Task<HttpResponseMessage> InvokeAsync(Action<double> updateCallback)
            {
                throw new NotImplementedException();
            }
        }
    }

    
}
