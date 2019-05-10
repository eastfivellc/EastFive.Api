using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using EastFive.Linq.Expressions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class RequestMessage<TResource>
        : 
            EastFive.Linq.Queryable<
                TResource, 
                RequestMessage<TResource>.RequestMessageProvideQuery>,
            IQueryable<TResource>,
            IRenderUrls
    {
        public class RequestMessageProvideQuery : 
            EastFive.Linq.QueryProvider<
                EastFive.Linq.Queryable<TResource,
                    EastFive.Api.RequestMessage<TResource>.RequestMessageProvideQuery>>
        {
            protected SendAsyncDelegate sendMessageDelegate;

            public RequestMessageProvideQuery(SendAsyncDelegate sendMessageDelegate) 
                : base(
                    (queryProvider, type) => new RequestMessage<TResource>(
                        (queryProvider as RequestMessageProvideQuery).sendMessageDelegate),
                    (queryProvider, expression, type) => new RequestMessage<TResource>(
                        (queryProvider as RequestMessageProvideQuery).sendMessageDelegate))
            {
                this.sendMessageDelegate = sendMessageDelegate;
            }

            public override object Execute(Expression expression)
            {
                return 1;
            }
        }

        protected SendAsyncDelegate sendMessageDelegate;

        public RequestMessage(SendAsyncDelegate sendMessageDelegate)
            : base(new RequestMessageProvideQuery(sendMessageDelegate))
        {
            this.sendMessageDelegate = sendMessageDelegate;
        }

        public IApplication Application { get; private set; }

        public HttpRequestMessage Request { get; private set; }

        public delegate Task<HttpResponseMessage> SendAsyncDelegate(HttpRequestMessage request);

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            return sendMessageDelegate(request);
        }

        internal RequestMessage<TResource> SetContent(HttpContent content)
        {
            this.Request.Content = content;
            return this;
        }

        public Uri RenderLocation(string routeName = "DefaultApi")
        {
            throw new NotImplementedException();
        }

        private static Uri AssignQueryExpressions<TResource>(Uri baseUri, IApplication application,
            Expression<Action<TResource>>[] parameters)
        {
            var queryParams = parameters
                .Select(
                    param =>
                    {
                        return param.GetUrlAssignment(
                            (propName, value) =>
                            {
                                var propertyValue = (string)application.CastResourceProperty(value, typeof(String));
                                return propName.PairWithValue(propertyValue);
                            });
                    })
                .Concat(baseUri.ParseQuery())
                .ToDictionary();

            var updatedUri = baseUri.SetQuery(queryParams);
            return updatedUri;
        }

        private static Uri AssignResourceToQuery<TResource>(Uri baseUri, IApplication application, TResource resource)
        {
            var queryParams = typeof(TResource)
                .GetMembers()
                .Where(member => member.MemberType == MemberTypes.Property || member.MemberType == MemberTypes.Field)
                .Select(
                    memberInfo =>
                    {
                        var propName = memberInfo.GetCustomAttribute<JsonPropertyAttribute, string>(
                            jsonAttr => jsonAttr.PropertyName,
                            () => memberInfo.Name);
                        var value = memberInfo.GetValue(resource);
                        var propertyValue = (string)application.CastResourceProperty(value, typeof(String));
                        return propName.PairWithValue(propertyValue);
                    })
                .Concat(baseUri.ParseQuery())
                .ToDictionary();

            var updatedUri = baseUri.SetQuery(queryParams);
            return updatedUri;
        }
    }
}
