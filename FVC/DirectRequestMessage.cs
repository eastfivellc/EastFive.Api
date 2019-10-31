using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    //public class DirectRequestMessage<TResource> : RequestMessage<TResource>
    //{
    //    public IApplication Application { get; private set; }

    //    public DirectRequestMessage(IApplication application, IInvokeApplication invokeApplication, HttpRequestMessage request)
    //        : base(invokeApplication, request)
    //    {
    //        this.Application = application;
    //    }

    //    public DirectRequestMessage(IApplication application, IInvokeApplication invokeApplication,
    //        HttpRequestMessage request, Expression expr)
    //        : base(invokeApplication, request, expr)
    //    {
    //        this.Application = application;
    //    }

    //    internal override RequestMessage<TResource> From()
    //    {
    //        return new DirectRequestMessage<TResource>(this.Application,
    //              this.InvokeApplication,
    //              this.Request);
    //    }

    //    internal override RequestMessage<TResource> FromExpression(Expression condition)
    //    {
    //        return new DirectRequestMessage<TResource>(this.Application,
    //              this.InvokeApplication,
    //              this.Request,
    //              condition);
    //    }
    //}
}
