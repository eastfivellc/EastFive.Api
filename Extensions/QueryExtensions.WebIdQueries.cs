using BlackBarLabs.Api.Resources;
using System;

namespace BlackBarLabs.Api
{
    public static partial class QueryExtensions
    {
        private class WebIdGuid : WebIdQuery
        {
            public Guid Guid { get; private set; }

            public WebIdGuid(Guid guid)
            {
                this.Guid = guid;
            }
        }

        private class WebIdGuids : WebIdQuery
        {
            public Guid [] Guids { get; private set; }

            public WebIdGuids(Guid [] guids)
            {
                this.Guids = guids;
            }
        }


        private class WebIdEmpty : WebIdQuery
        {
        }

        private class WebIdBadRequest : WebIdQuery
        {
        }

        private class QueryUnspecified : IWebParsable
        {
            public bool IsSpecified()
            {
                return false;
            }
        }

        private class WebIdObject : WebIdQuery
        {
            public ResourceQueryBase Obj { get; private set; }

            public WebIdObject(ResourceQueryBase obj)
            {
                this.Obj = obj;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        private class QueryParameterTypeAttribute : System.Attribute
        {
            public QueryParameterTypeAttribute()
            {
            }

            private Type webIdQueryType;
            public Type WebIdQueryType
            {
                get
                {
                    return this.webIdQueryType;
                }
                set
                {
                    webIdQueryType = value;
                }
            }
        }

        [QueryParameterType(WebIdQueryType = typeof(WebIdGuid))]
        public static Guid ParamSingle(this WebIdQuery query)
        {
            if (!(query is WebIdGuid))
                throw new InvalidOperationException("Do not use ParamSingle outside of ParseAsync");

            var wiqo = query as WebIdGuid;
            return wiqo.Guid;
        }

        [QueryParameterType(WebIdQueryType = typeof(WebIdEmpty))]
        public static Guid? ParamEmpty(this WebIdQuery query)
        {
            if (!(query is WebIdEmpty))
                throw new InvalidOperationException("Do not use ParamEmpty outside of ParseAsync");
            return default(Guid?);
        }

        [QueryParameterType(WebIdQueryType = typeof(WebIdGuids))]
        public static Guid[] ParamOr(this WebIdQuery query)
        {
            if (!(query is WebIdGuids))
                throw new InvalidOperationException("Do not use ParamOr outside of ParseAsync");

            var wiqo = query as WebIdGuids;
            return wiqo.Guids;
        }
    }
}
