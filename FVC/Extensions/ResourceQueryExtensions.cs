﻿using System;
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
using EastFive.Linq;

namespace EastFive.Api
{
    public static class ResourceQueryExtensions
    {
        [AttributeUsage(AttributeTargets.Method)]
        public class BinaryComparisonQueryAttribute : Attribute, IBuildHttpRequests, IBuildUrls
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

                            if (typeof(Guid).IsAssignableFrom(memberType))
                            {
                                var contentId = (Guid)content;
                                return contentId.ToString();
                            }
                            if(memberType.IsSubClassOfGeneric(typeof(IReferenceable)))
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

                            if (typeof(DateTime).IsAssignableFrom(memberType))
                            {
                                var contentDate = (DateTime)content;
                                return contentDate.ToString();
                            }

                            if (typeof(Type) == memberType)
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

            public HttpRequestMessage MutateRequest(HttpRequestMessage request,
                MethodInfo method, Expression[] arguments)
            {
                request.RequestUri = BindUrlQueryValue(request.RequestUri, method, arguments);
                return request;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class MutateIdQueryAttribute : Attribute, IBuildHttpRequests, IBuildUrls
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
                MethodInfo method, Expression[] arguments)
            {
                request.RequestUri = BindUrlQueryValue(request.RequestUri, method, arguments);
                return request;
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
            if (!typeof(RequestMessage<TResource>).IsAssignableFrom(query.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
            var requestMessageQuery = query as RequestMessage<TResource>;

            var condition = Expression.Call(
                typeof(ResourceQueryExtensions), "ById", new Type[] { typeof(TResource) },
                query.Expression, Expression.Constant(resourceId));

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }


        [MutateIdQuery]
        public static IQueryable<TResource> ById<TResource>(this IQueryable<TResource> query, IRef<TResource> resourceRef)
            where TResource : IReferenceable
        {
            if (!typeof(RequestMessage<TResource>).IsAssignableFrom(query.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
            var requestMessageQuery = query as RequestMessage<TResource>;

            var condition = Expression.Call(
                typeof(ResourceQueryExtensions), "ById", new Type[] { typeof(TResource) },
                query.Expression, Expression.Constant(resourceRef.id));

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        [MutateIdQuery]
        public static IQueryable<TResource> ById<TResource>(this IQueryable<TResource> query, IRefOptional<TResource> resourceRef)
            where TResource : IReferenceable
        {
            if (!resourceRef.HasValue)
                return query;

            if (!typeof(IComposibleQuery<TResource>).IsAssignableFrom(query.GetType()))
                throw new ArgumentException($"Query must be of type `{typeof(IComposibleQuery<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
            var composibleQuery = query as IComposibleQuery<TResource>;

            var condition = Expression.Call(
                typeof(ResourceQueryExtensions), "ById", new Type[] { typeof(TResource) },
                query.Expression, Expression.Constant(resourceRef.id.Value));

            var requestMessageNewQuery = composibleQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class QueryParamQueryAttribute : Attribute, IBuildHttpRequests, IBuildUrls
        {
            public Uri BindUrlQueryValue(Uri url, MethodInfo method, Expression[] arguments)
            {
                var key = (string)arguments[0].Resolve();
                var value = (string)arguments[1].Resolve();

                return url.AddQueryParameter(key, value);

                //return arguments
                //    .Zip(
                //        method.GetParameters(),
                //        (arg, paramInfo) =>
                //        {
                //            KeyValuePair<string, object> Evaluate(Expression expr)
                //            {
                //                if (expr is UnaryExpression)
                //                {
                //                    var argUnary = expr as UnaryExpression;
                //                    return Evaluate(argUnary.Operand);
                //                }
                //                if (expr is Expression<Func<object>>)
                //                {
                //                    var paramExpr = expr as Expression<Func<object>>;
                //                    return Evaluate(paramExpr.Body);
                //                }
                //                if(expr is MemberExpression)
                //                {
                //                    var memberExpr = expr as MemberExpression;
                //                    var paramName = memberExpr.Member.Name;
                //                    var paramValue = arg.Resolve();
                //                    return paramName.PairWithValue(paramValue);
                //                }
                //                if (expr is BinaryExpression)
                //                {
                //                    var memberExpr = expr as BinaryExpression;
                //                    if (memberExpr.Left is ConstantExpression)
                //                    {
                //                        var left = memberExpr.Left as ConstantExpression;
                //                        var paramName = left.Type == typeof(string)?
                //                            left.Value as string
                //                            :
                //                            (string)left.Resolve();
                //                        var paramValue = memberExpr.Right.Resolve();
                //                        return paramName.PairWithValue(paramValue);
                //                    }
                //                }
                //                {
                //                    var paramValue = expr.Resolve();
                //                    return paramInfo.Name.PairWithValue(paramValue);
                //                }
                //            }
                //            return Evaluate(arg);
                //        })
                //    .Aggregate(url,
                //        (urlAggr, kvp) =>
                //        {
                //            return urlAggr.AddQueryParameter(kvp.Key, kvp.Value.ToString());
                //        });
            }

            public HttpRequestMessage MutateRequest(HttpRequestMessage request,
                MethodInfo method, Expression[] arguments)
            {
                request.RequestUri = BindUrlQueryValue(request.RequestUri, method, arguments);
                return request;
            }
        }

        [QueryParamQuery]
        public static IQueryable<TResource> QueryParam<TResource>(this IQueryable<TResource> query, string key, string value)
            where TResource : IReferenceable
        {
            if (!typeof(RequestMessage<TResource>).IsAssignableFrom(query.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
            var requestMessageQuery = query as RequestMessage<TResource>;

            var methodInfo = typeof(ResourceQueryExtensions)
                .GetMethod("QueryParam", BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(TResource));
            var condition = Expression.Call(methodInfo, query.Expression, 
                Expression.Constant(key), Expression.Constant(value));

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        //[QueryParamQuery]
        //public static IQueryable<TResource> QueryParam<TResource>(this IQueryable<TResource> query, Expression<bool> paramExpr)
        //    where TResource : IReferenceable
        //{
        //    if (!typeof(RequestMessage<TResource>).IsAssignableFrom(query.GetType()))
        //        throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
        //    var requestMessageQuery = query as RequestMessage<TResource>;

        //    var condition = Expression.Call(
        //        typeof(ResourceQueryExtensions), "QueryParam", new Type[] { typeof(TResource) },
        //        query.Expression, paramExpr);

        //    var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
        //    return requestMessageNewQuery;
        //}
    }
}
