using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using EastFive.Collections.Generic;
using EastFive.Reflection;

namespace EastFive.Api
{
    public class UrlBuilder : IBuildUrls
    {
        private UrlHelper urlHelper;
        private HttpApplication httpApp;

        public UrlBuilder(HttpRequestMessage request, HttpApplication httpApp)
        {
            this.httpApp = httpApp;
            this.urlHelper = new UrlHelper(request);
        }

        public IQueryable<T> Resources<T>()
        {
            var queryProvider = new RenderableQueryProvider(this.urlHelper, this.httpApp);
            return new RenderableQuery<T>(queryProvider);
        }

        private class RenderableQueryProvider : IQueryProvider
        {
            private UrlHelper urlHelper;
            private HttpApplication httpApp;

            public RenderableQueryProvider(UrlHelper urlHelper, HttpApplication httpApp)
            {
                this.httpApp = httpApp;
                this.urlHelper = urlHelper;
            }

            public IQueryable CreateQuery(Expression expression)
            {
                Type elementType = TypeSystem.GetElementType(expression.Type);
                try
                {
                    return (IQueryable)Activator.CreateInstance(
                        typeof(RenderableQuery<>).MakeGenericType(elementType),
                        new object[] { this, expression });
                }
                catch (TargetInvocationException tie)
                {
                    throw tie.InnerException;
                }
            }

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            {
                return new RenderableQuery<TElement>(this, expression);
            }

            public TResult Execute<TResult>(Expression expression)
            {
                return (TResult)this.Execute(expression);
            }

            public Uri RenderLocation(Expression expression, string routeName = "DefaultApi")
            {
                var baseUrl = urlHelper.GetLocation(expression.Type.GenericTypeArguments.First(), routeName);
                var queryParams = new QueryTranslator(httpApp).Translate(expression);
                //var queryParams = expression
                //    .Select(param => param.GetUrlAssignment(
                //        (queryParamName, value) =>
                //        {
                //            return queryParamName
                //                .PairWithValue((string)application.CastResourceProperty(value, typeof(String)));
                //        }))
                //    .ToDictionary();

                var queryUrl = baseUrl.SetQuery(queryParams);
                return queryUrl;
            }

            private static IDictionary<string, string> Translate(Expression expression)
            {
                if (expression is MethodCallExpression)
                {
                    var methodCallExpression = expression as MethodCallExpression;
                    if (methodCallExpression.Method.DeclaringType == typeof(Queryable) && methodCallExpression.Method.Name == "Where")
                    {
                        //var queryParam = methodCallExpression 
                        //LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                        //this.Visit(lambda.Body);
                        //return m;
                    }
                }
                return null;
            }

            public object Execute(Expression expression)
            {
                throw new NotImplementedException();
            }
        }

        private class RenderableQuery<T> : IQueryable<T>, IRenderUrls
        {
            private RenderableQueryProvider provider;

            public RenderableQuery(RenderableQueryProvider provider)
            {
                this.provider = provider;
                this.Expression = Expression.Constant(this);
            }

            public RenderableQuery(RenderableQueryProvider provider, Expression expression)
            {
                this.provider = provider;

                if (expression == null)
                    throw new ArgumentNullException("expression");
                if (!typeof(IQueryable<T>).IsAssignableFrom(expression.Type))
                    throw new ArgumentOutOfRangeException("expression");
                this.Expression = expression;
            }

            public Expression Expression { get; }

            public Type ElementType => typeof(T);

            public IQueryProvider Provider => this.provider;

            public IEnumerator<T> GetEnumerator()
            {
                return ((IEnumerable<T>)this.provider.Execute(this.Expression)).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)this.provider.Execute(this.Expression)).GetEnumerator();
            }

            public override string ToString()
            {
                return this.RenderLocation().AbsoluteUri;
            }

            public Uri RenderLocation(string routeName = "DefaultApi")
            {
                return this.provider.RenderLocation(this.Expression, routeName);
            }
        }

        internal class QueryTranslator : ExpressionVisitor
        {
            List<KeyValuePair<string, string>> queryParams;
            HttpApplication application;

            internal QueryTranslator(HttpApplication application)
            {
                this.application = application;
            }

            internal IDictionary<string, string> Translate(Expression expression)
            {
                this.queryParams = new List<KeyValuePair<string, string>>();
                this.Visit(expression);
                return this.queryParams.ToDictionary();
            }

            private static Expression StripQuotes(Expression e)
            {
                while (e.NodeType == ExpressionType.Quote)
                {
                    e = ((UnaryExpression)e).Operand;
                }
                return e;
            }

            protected override Expression VisitMethodCall(MethodCallExpression m)
            {
                
                throw new NotSupportedException(string.Format("The method '{0}' is not supported", m.Method.Name));
            }

            protected override Expression VisitUnary(UnaryExpression u)
            {
                switch (u.NodeType)
                {
                    case ExpressionType.Not:
                        this.Visit(u.Operand);
                        break;
                    default:
                        throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", u.NodeType));
                }
                return u;
            }

            protected override Expression VisitBinary(BinaryExpression b)
            {
                this.Visit(b.Left);
                switch (b.NodeType)
                {
                    case ExpressionType.And:
                        break;
                    case ExpressionType.Or:
                        break;
                    case ExpressionType.Equal:
                        break;
                    case ExpressionType.NotEqual:
                        break;
                    case ExpressionType.LessThan:
                        break;
                    case ExpressionType.LessThanOrEqual:
                        break;
                    case ExpressionType.GreaterThan:
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        break;
                    default:
                        throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));
                }
                this.Visit(b.Right);
                return b;
            }

            protected override Expression VisitConstant(ConstantExpression c)
            {
                IQueryable q = c.Value as IQueryable;
                if (q != null)
                {
                    // assume constant nodes w/ IQueryables are table references
                }
                else if (c.Value == null)
                {

                }
                else
                {
                    switch (Type.GetTypeCode(c.Value.GetType()))
                    {
                        case TypeCode.Boolean:
                            //sb.Append(((bool)c.Value) ? 1 : 0);
                            break;

                        case TypeCode.String:
                            //sb.Append(c.Value);
                            break;

                        case TypeCode.Object:
                            throw new NotSupportedException(string.Format("The constant for '{0}' is not supported", c.Value));

                        default:
                            //sb.Append(c.Value);
                            break;
                    }
                }
                return c;
            }

            protected override Expression VisitMember(MemberExpression m)
            {
                if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter)
                {
                    return m;
                }
                return m;
            }
        }



        internal static class TypeSystem
        {


            internal static Type GetElementType(Type seqType)
            {


                Type ienum = FindIEnumerable(seqType);


                if (ienum == null) return seqType;


                return ienum.GetGenericArguments()[0];


            }


            private static Type FindIEnumerable(Type seqType)
            {


                if (seqType == null || seqType == typeof(string))


                    return null;


                if (seqType.IsArray)


                    return typeof(IEnumerable<>).MakeGenericType(seqType.GetElementType());


                if (seqType.IsGenericType)
                {


                    foreach (Type arg in seqType.GetGenericArguments())
                    {


                        Type ienum = typeof(IEnumerable<>).MakeGenericType(arg);


                        if (ienum.IsAssignableFrom(seqType))
                        {


                            return ienum;


                        }


                    }


                }


                Type[] ifaces = seqType.GetInterfaces();


                if (ifaces != null && ifaces.Length > 0)
                {


                    foreach (Type iface in ifaces)
                    {


                        Type ienum = FindIEnumerable(iface);


                        if (ienum != null) return ienum;


                    }


                }


                if (seqType.BaseType != null && seqType.BaseType != typeof(object))
                {


                    return FindIEnumerable(seqType.BaseType);


                }


                return null;


            }


        }
    }
}
