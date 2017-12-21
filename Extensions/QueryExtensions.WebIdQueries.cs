using BlackBarLabs.Api.Resources;
using BlackBarLabs.Extensions;
using System;
using System.Linq;

namespace BlackBarLabs.Api
{

    public static partial class QueryExtensions
    {
        [QueryParameterType(WebIdQueryType = typeof(WebIdGuid))]
        public static Guid ParamSingle(this WebIdQuery query)
        {
            return query.ParseInternal(
                (v) =>
                {
                    if (!(v is WebIdGuid))
                        throw new InvalidOperationException("Do not use ParamSingle outside of ParseAsync");

                    var wiqo = v as WebIdGuid;
                    return wiqo.Guid;
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }
        
        [QueryParameterType(WebIdQueryType = typeof(WebIdMaybe), IsOptional = true)]
        public static Guid? ParamMaybe(this WebIdQuery query)
        {
            if (query.IsDefault())
                return default(Guid?);
            
            return query.ParseInternal(
                (v) =>
                {
                    if (!(v is WebIdMaybe))
                        throw new InvalidOperationException("Do not use ParamMaybe outside of ParseAsync");

                    var wiqo = v as WebIdMaybe;
                    return wiqo.GuidMaybe;
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }

        [QueryParameterType(WebIdQueryType = typeof(WebIdAny))]
        public static bool ParamAny(this WebIdQuery query)
        {
            return query.ParseInternal(
                (v) =>
                {
                    if (!(v is WebIdAny))
                        throw new InvalidOperationException("Do not use ParamSingle outside of ParseAsync");

                    var wiqo = v as WebIdAny;
                    return true;
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }

        [QueryParameterType(WebIdQueryType = typeof(WebIdEmpty))]
        public static Guid? ParamEmpty(this WebIdQuery query)
        {
            return query.ParseInternal(
                (v) =>
                {
                    if (!(v is WebIdEmpty))
                        throw new InvalidOperationException("Do not use ParamEmpty outside of ParseAsync");
                    return default(Guid?);
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }

        [QueryParameterType(WebIdQueryType = typeof(WebIdGuids))]
        public static Guid[] ParamOr(this WebIdQuery query)
        {
            return query.ParseInternal(
                (v) =>
                {
                    if (!(v is WebIdGuids))
                        throw new InvalidOperationException("Do not use ParamEmpty outside of ParamOr");

                    var wiqo = v as WebIdGuids;
                    return wiqo.Guids;
                },
                (why) =>
                {
                    throw new InvalidOperationException("Use ParseAsync to ensure parsable values");
                });
        }

        class WebIdMaybe : QueryMatchAttribute
        {
            public Guid? GuidMaybe { get; private set; }

            public WebIdMaybe(Guid? guid)
            {
                this.GuidMaybe = guid;
            }
        }

        class WebIdGuid : WebIdMaybe
        {
            public Guid Guid { get; private set; }

            public WebIdGuid(Guid guid) : base(guid)
            {
                this.Guid = guid;
            }
        }

        class WebIdGuids : QueryMatchAttribute
        {
            public Guid[] Guids { get; private set; }

            public WebIdGuids(Guid[] guids)
            {
                this.Guids = guids;
            }
        }

        class WebIdEmpty : WebIdMaybe
        {
            public WebIdEmpty() : base(default(Guid?))
            {
            }
        }

        class WebIdAny : WebIdMaybe
        {
            public WebIdAny() : base(default(Guid?))
            {
            }
        }

        internal static TResult ParseInternal<TResult>(this WebIdQuery query,
            Func<QueryMatchAttribute, TResult> parsed,
            Func<string, TResult> unparsable)
        {
            if (default(WebIdQuery) == query)
                return parsed(new WebIdEmpty());
            return query.Parse(
                (value) => parsed(new WebIdGuid(value)),
                (values) => parsed(new WebIdGuids(values.ToArray())),
                () => parsed(new WebIdEmpty()),
                () => parsed(new WebIdEmpty()),
                () => parsed(new WebIdAny()),
                () => unparsable($"Could not parse WebId from {query}"));
        }

    }
}
