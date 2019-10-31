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
        public RequestMessage(IInvokeApplication invokeApplication)
            : base(new RequestMessageProvideQuery(invokeApplication))
        {
            this.InvokeApplication = invokeApplication;
        }

        public RequestMessage(IInvokeApplication invokeApplication, Expression expr)
            : base(new RequestMessageProvideQuery(invokeApplication), expr)
        {
            this.InvokeApplication = invokeApplication;
        }

        public IInvokeApplication InvokeApplication { get; private set; }

        public RequestMessage<TRelatedResource> Related<TRelatedResource>()
        {
            return new RequestMessage<TRelatedResource>(this.InvokeApplication);
        }

        public class RequestMessageProvideQuery :
            EastFive.Linq.QueryProvider<
                EastFive.Linq.Queryable<TResource,
                    EastFive.Api.RequestMessage<TResource>.RequestMessageProvideQuery>>
        {
            public RequestMessageProvideQuery(IInvokeApplication invokeApplication)
                : base(
                    (queryProvider, type) => (queryProvider is RequestMessage<TResource>)?
                        (queryProvider as RequestMessage<TResource>).From()
                        :
                        new RequestMessage<TResource>(invokeApplication),
                    (queryProvider, expression, type) => (queryProvider is RequestMessage<TResource>) ?
                        (queryProvider as RequestMessage<TResource>).FromExpression(expression)
                        :
                        new RequestMessage<TResource>(invokeApplication, expression))
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
                  condition);
        }

        internal virtual RequestMessage<TResource> From()
        {
            return new RequestMessage<TResource>(
                  this.InvokeApplication);
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
            var requestMessage = Activator.CreateInstance(type, invokeApp);
            return onSuccess(requestMessage);
        }
    }
}
