using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using EastFive.Linq;
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
    [RequestMessage]
    public class RequestMessage<TResource>
        : 
            EastFive.Linq.Queryable<
                TResource, 
                RequestMessage<TResource>.RequestMessageProvideQuery>,
            IQueryable<TResource>,
            Linq.ISupplyQueryProvider<RequestMessage<TResource>>
    {
        public RequestMessage(IInvokeApplication invokeApplication, HttpRequestMessage request)
            : base(new RequestMessageProvideQuery(invokeApplication, request))
        {
            this.InvokeApplication = invokeApplication;
            this.Request = request;
        }

        public RequestMessage(IInvokeApplication invokeApplication, HttpRequestMessage request, Expression expr)
            : base(new RequestMessageProvideQuery(invokeApplication, request), expr)
        {
            this.InvokeApplication = invokeApplication;
            this.Request = request;
        }

        public IInvokeApplication InvokeApplication { get; private set; }

        public HttpRequestMessage Request { get; private set; }

        public RequestMessage<TRelatedResource> Related<TRelatedResource>()
        {
            return new RequestMessage<TRelatedResource>(this.InvokeApplication, this.Request);
        }

        internal RequestMessage<TResource> SetContent(HttpContent content)
        {
            this.Request.Content = content;
            return this;
        }

        public class RequestMessageProvideQuery :
            EastFive.Linq.QueryProvider<
                EastFive.Linq.Queryable<TResource,
                    EastFive.Api.RequestMessage<TResource>.RequestMessageProvideQuery>>
        {
            public RequestMessageProvideQuery(IInvokeApplication invokeApplication, HttpRequestMessage request)
                : base(
                    (queryProvider, type) => (queryProvider is RequestMessage<TResource>)?
                        (queryProvider as RequestMessage<TResource>).From()
                        :
                        new RequestMessage<TResource>(invokeApplication, request),
                    (queryProvider, expression, type) => (queryProvider is RequestMessage<TResource>) ?
                        (queryProvider as RequestMessage<TResource>).FromExpression(expression)
                        :
                        new RequestMessage<TResource>(invokeApplication, request, expression))
            {
            }

            public override object Execute(Expression expression)
            {
                return 1;
            }
        }

        internal virtual RequestMessage<TResource> FromExpression(Expression condition)
        {
            return new RequestMessage<TResource>(
                  this.InvokeApplication,
                  this.Request,
                  condition);
        }

        internal virtual RequestMessage<TResource> From()
        {
            return new RequestMessage<TResource>(
                  this.InvokeApplication,
                  this.Request);
        }

        public RequestMessage<TResource> ActivateQueryable(QueryProvider<RequestMessage<TResource>> provider, Type type)
        {
            return From();
        }

        public RequestMessage<TResource> ActivateQueryableWithExpression(QueryProvider<RequestMessage<TResource>> queryProvider,
            Expression expression, Type elementType)
        {
            return FromExpression(expression);
        }
    }

    public class RequestMessageAttribute : Attribute, IInstigatableGeneric
    {
        public virtual Task<HttpResponseMessage> InstigatorDelegateGeneric(Type type,
            HttpApplication httpApp, HttpRequestMessage request, ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            var invokeApp = IInvokeApplicationAttribute.Instigate(httpApp, request);
            var requestMessage = Activator.CreateInstance(type, invokeApp, request);
            return onSuccess(requestMessage);
        }
    }
}
