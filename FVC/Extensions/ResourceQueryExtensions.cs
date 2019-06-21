using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Reflection;
using EastFive.Linq.Expressions;
using System.Net.Http;
using BlackBarLabs.Extensions;
using EastFive.Extensions;

namespace EastFive.Api
{
    public static class ResourceQueryExtensions
    {
        [AttributeUsage(AttributeTargets.Method)]
        public class BinaryComparisonQueryAttribute : Attribute, IFilterApiValues
        {
            public Uri BindUrlQueryValue(Uri url, MethodInfo method, Expression[] arguments)
            {
                return method.TryParseMemberAssignment(arguments,
                    (memberInfo, type, content) =>
                    {
                        var queryParamName = GetParamName();
                        var queryParamValue = GetContent(type);
                        return url.AddQueryParameter(queryParamName, queryParamValue);

                        string GetParamName()
                        {
                            var apiBindings = memberInfo.GetAttributesInterface<IProvideApiValue>(true);
                            if (apiBindings.Any())
                            {
                                var apiBinding = apiBindings.First();
                                if (apiBinding.PropertyName.HasBlackSpace())
                                    return apiBinding.PropertyName;
                            }
                            return memberInfo.Name;
                        }

                        string GetContent(ExpressionType expressionType)
                        {
                            if(expressionType == ExpressionType.Not)
                            {
                                if (content == null)
                                    return "not(null)";
                                var contentType = content.GetType();

                                if(contentType == typeof(bool))
                                {
                                    var boolContent = (bool)content;
                                    return (!boolContent).ToString();
                                }

                                if (contentType == typeof(string))
                                {
                                    var stringContent = content as string;
                                    if (stringContent.ToLower() == "value")
                                        return "null";
                                    if (stringContent.ToLower() == "not(null)")
                                        return "null";
                                    if (stringContent.ToLower() == "null")
                                        return "not(null)";
                                    return $"not({stringContent})";
                                }

                                var innerContent = GetContent(ExpressionType.Equal);
                                return $"not({innerContent})";
                            }

                            var memberType = memberInfo.GetMemberType();
                            if (typeof(string).IsAssignableFrom(memberType))
                            {
                                return (string)content;
                            }

                            if (typeof(Guid).IsAssignableFrom(memberType) ||
                                memberType.IsSubClassOfGeneric(typeof(IReferenceable)))
                            {
                                if (content.IsDefaultOrNull())
                                    return string.Empty;
                                var contentType = content.GetType();
                                if (contentType.IsSubClassOfGeneric(typeof(IReferenceable)))
                                {
                                    var contentRef = (IReferenceable)content;
                                    return contentRef.id.ToString();
                                }
                            }

                            if(typeof(Type) == memberType)
                            {
                                return (content as Type).GetCustomAttributes<FunctionViewControllerAttribute>()
                                    .First().ContentType;
                            }

                            throw new NotImplementedException();
                        }
                    },
                    () =>
                    {
                        throw new Exception();
                    });
            }

            public HttpRequestMessage MutateRequest(HttpRequestMessage request, HttpMethod httpMethod,
                MethodInfo method, Expression[] arguments)
            {
                request.RequestUri = BindUrlQueryValue(request.RequestUri, method, arguments);
                return request;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class MutateIdQueryAttribute : Attribute, IFilterApiValues
        {
            public Uri BindUrlQueryValue(Uri url, MethodInfo method, Expression[] arguments)
            {
                var queryParameters = arguments
                    .Zip(
                        method.GetParameters(),
                        (arg, paramInfo) => paramInfo.PairWithValue(arg))
                    .Select(argument => argument.Key.PairWithValue(argument.Value.Resolve()))
                    .ToArray();
                var idStr = ((Guid)queryParameters.First().Value).ToString("N");
                return url.AppendToPath(idStr);
            }

            public HttpRequestMessage MutateRequest(HttpRequestMessage request,
                HttpMethod httpMethod, MethodInfo method, Expression[] arguments)
            {
                throw new NotImplementedException();
            }
        }

        private class Provider<TResource> : Linq.QueryProvider<
                EastFive.Linq.Queryable<TResource, Provider<TResource>>>
        {
            public Provider(Guid resourceId) : base(
                (func, type) => new Linq.Queryable<TResource, Provider<TResource>>(new Provider<TResource>(resourceId)), 
                (func, expression, type) =>
                {
                    var condition = Expression.Call(
                        typeof(ResourceQueryExtensions), "ById", new Type[] { typeof(TResource) },
                        expression, Expression.Constant(resourceId));
                    //var predicate = Expression.Lambda<Func<TResource, bool>>(condition, valueSelector.Parameters);
                    return new Linq.Queryable<TResource, Provider<TResource>>(
                        new Provider<TResource>(resourceId), expression);
                })
            {

            }

            public override object Execute(Expression expression)
            {
                throw new NotImplementedException();
            }
        }

        [MutateIdQuery]
        public static IQueryable<TResource> ById<TResource>(this IQueryable<TResource> query, Guid resourceId)
            where TResource : IReferenceable
        {
            //return query.Where(res => res.id == resourceId);

            //var queryable = new EastFive.Linq.Queryable<TResource, Provider<TResource>>(
            //    new Provider<TResource>(resourceId));

            //Expression<Func<IQueryable<TResource>, IQueryable<TResource>>> expr = q =>
            //    q.Where(res => res.id == resourceId);

            if (!typeof(RequestMessage<TResource>).IsAssignableFrom(query.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
            var requestMessageQuery = query as RequestMessage<TResource>;

            var condition = Expression.Call(
                typeof(ResourceQueryExtensions), "ById", new Type[] { typeof(TResource) },
                query.Expression, Expression.Constant(resourceId));

            var requestMessageNewQuery = new RequestMessage<TResource>(
                requestMessageQuery.Application, requestMessageQuery.Request, condition);
            return requestMessageNewQuery;
        }
    }
}
