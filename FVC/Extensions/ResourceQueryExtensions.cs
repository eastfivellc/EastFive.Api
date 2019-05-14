using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Reflection;
using EastFive.Linq.Expressions;

namespace EastFive.Api
{
    public static class ResourceQueryExtensions
    {
        [AttributeUsage(AttributeTargets.Method)]
        public class MutateIdQueryAttribute : Attribute, IMutateQuery
        {
            public UriBuilder GetQueryParameters(UriBuilder urlBuilder, MethodCallExpression expr)
            {
                var arguments = expr.ResolveArgs();
                var idStr = ((Guid)arguments[1].Value).ToString("N");
                if (urlBuilder.Path.EndsWith("/"))
                {
                    urlBuilder.Path = urlBuilder.Path + idStr;
                    return urlBuilder;
                }

                if (urlBuilder.Query.HasBlackSpace())
                {
                    urlBuilder.Query += urlBuilder.Query + $"&id=idStr";
                    return urlBuilder;
                }

                urlBuilder.Path = urlBuilder.Path + "/" + idStr;
                return urlBuilder;
            }
        }

        [MutateIdQuery]
        public static IQueryable<TResource> ById<TResource>(this IQueryable<TResource> query, Guid resourceId)
            where TResource : IReferenceable
        {
            return query.Where(res => res.id == resourceId);
        }
    }
}
