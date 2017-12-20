using BlackBarLabs.Linq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net.Http;
using System.Threading.Tasks;
using EastFive.Linq;

namespace BlackBarLabs.Api
{
    public static partial class QueryExtensions
    {
        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat1)
            where TQuery : ResourceQueryBase
        {
            var queries = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage>>>>).NullToEmpty();
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat1 };
            return await ParseAsync(query, request, queries, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat2)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage>>>>).NullToEmpty();
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat1, queryFormat2 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage>>>>).NullToEmpty();
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat1, queryFormat2, queryFormat3 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1)
            where TQuery : ResourceQueryBase
        {
            var queries = new[] { queryFormat1 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>>).NullToEmpty();
            return await ParseAsync(query, request, queries, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat2)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat2 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat2, queryFormat3 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat2, queryFormat3, queryFormat4 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }
        
        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat5)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat2, queryFormat3, queryFormat4, queryFormat5 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat5,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat6)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat2, queryFormat3, queryFormat4, queryFormat5, queryFormat6 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat5,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat6,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat7)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat2, queryFormat3, queryFormat4, queryFormat5, queryFormat6, queryFormat7 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat5,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat6,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat7,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat8)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat2, queryFormat3, queryFormat4, queryFormat5, queryFormat6, queryFormat7, queryFormat8 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2)
            where TQuery : ResourceQueryBase
        {
            var queries = new[] { queryFormat1, queryFormat2 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>>).NullToEmpty();
            return await ParseAsync(query, request, queries, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1, queryFormat2 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat3 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1, queryFormat2 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat3, queryFormat4 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }
        
        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat5,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat6)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1, queryFormat2 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat3, queryFormat4, queryFormat5, queryFormat6,  };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat5,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat6,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat7)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1, queryFormat2 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat3, queryFormat4, queryFormat5, queryFormat6, queryFormat7 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat3)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1, queryFormat2, queryFormat3 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>>).NullToEmpty();
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat5)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1, queryFormat2, queryFormat3 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat4, queryFormat5 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat5,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat6)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1, queryFormat2, queryFormat3 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat4, queryFormat5, queryFormat6 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat4,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat5,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat6,
            Expression<Func<TQuery, Task<HttpResponseMessage[]>>> queryFormat7)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = new[] { queryFormat1, queryFormat2, queryFormat3 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = new[] { queryFormat4, queryFormat5, queryFormat6, queryFormat7 };
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
           Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
           Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
           Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat3,
           Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat4)
           where TQuery : ResourceQueryBase
        {
            var queries = new[] { queryFormat1, queryFormat2, queryFormat3, queryFormat4 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>>).NullToEmpty();
            return await ParseAsync(query, request, queries, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat3,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat4,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat5,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat6)
            where TQuery : ResourceQueryBase
        {
            var queries = new[] { queryFormat1, queryFormat2, queryFormat3, queryFormat4, queryFormat5, queryFormat6 };
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>>).NullToEmpty();
            return await ParseAsync(query, request, queries, queriesEnumerable, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat1,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat2,
            Expression<Func<TQuery, Task<HttpResponseMessage>>> queryFormat3,
            Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>> queryFormat4,
            Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>> queryFormat5,
            Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>> queryFormat6)
            where TQuery : ResourceQueryBase
        {
            var queries1 = new[] { queryFormat1, queryFormat2, queryFormat3 };
            var queries2 = new[] { queryFormat4, queryFormat5, queryFormat6 };
            var queriesArray = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>>).NullToEmpty();
            return await ParseAsync(query, request, queries1, queries2, queriesArray);
        }

        public static async Task<HttpResponseMessage> ParseAsync<TQuery>(this TQuery query, HttpRequestMessage request,
            Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>> queryFormat1,
            Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>> queryFormat2,
            Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>> queryFormat3,
            Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>> queryFormat4,
            Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>> queryFormat5,
            Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>> queryFormat6)
            where TQuery : ResourceQueryBase
        {
            var queriesSingle = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage>>>>).NullToEmpty();
            var queriesEnumerable = default(IEnumerable<Expression<Func<TQuery, Task<IEnumerable<HttpResponseMessage>>>>>).NullToEmpty();
            var queriesArray = default(IEnumerable<Expression<Func<TQuery, Task<HttpResponseMessage[]>>>>).NullToEmpty();
            return await ParseAsync(query, request, queriesSingle, queriesEnumerable, queriesArray);
        }
    }
}
