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
        public RequestMessage(IApplication application, HttpRequestMessage request)
            : base(new RequestMessageProvideQuery(application, request))
        {
            this.Application = application;
            this.Request = request;
        }

        public RequestMessage(IApplication application, HttpRequestMessage request, Expression expr)
            : base(new RequestMessageProvideQuery(application, request), expr)
        {
            this.Application = application;
            this.Request = request;
        }

        public IApplication Application { get; private set; }

        public HttpRequestMessage Request { get; private set; }

        public RequestMessage<TRelatedResource> Related<TRelatedResource>()
        {
            return new RequestMessage<TRelatedResource>(this.Application, this.Request);
        }

        internal RequestMessage<TResource> SetContent(HttpContent content)
        {
            this.Request.Content = content;
            return this;
        }

        public Uri RenderLocation(string routeName = "DefaultApi")
        {
            var expr = this.Expression;
            var provider = this.Provider as RequestMessageProvideQuery;
            provider.Execute<TResource>(expr);
            return provider.RenderLocation(expr, routeName);
        }

        public class RequestMessageProvideQuery :
            EastFive.Linq.QueryProvider<
                EastFive.Linq.Queryable<TResource,
                    EastFive.Api.RequestMessage<TResource>.RequestMessageProvideQuery>>
        {
            private IApplication application;

            private HttpRequestMessage request;

            public RequestMessageProvideQuery(IApplication application, HttpRequestMessage request)
                : base(
                    (queryProvider, type) => new RequestMessage<TResource>(application, request),
                    (queryProvider, expression, type) => new RequestMessage<TResource>(application, request, expression))
            {
            }

            public override object Execute(Expression expression)
            {
                return 1;
            }

            public Uri RenderLocation(Expression expression, string routeName = "DefaultApi")
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
}
