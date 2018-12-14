using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpVerbAttribute : Attribute
    {
        private bool matchAllParameters = true;
        public bool MatchAllParameters
        {
            get
            {
                return matchAllParameters;
            }
            set
            {
                matchAllParameters = value;
                matchAllBodyParameters = value;
                matchAllQueryParameters = value;
            }
        }

        private bool matchAllBodyParameters = false;
        public bool MatchAllBodyParameters
        {
            get
            {
                return matchAllBodyParameters;
            }
            set
            {
                if (!value)
                    matchAllParameters = false;
                matchAllBodyParameters = value;
            }
        }

        private bool matchAllQueryParameters = true;
        public bool MatchAllQueryParameters
        {
            get
            {
                return matchAllQueryParameters;
            }
            set
            {
                if (!value)
                    matchAllQueryParameters = false;
                matchAllQueryParameters = value;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpBodyAttribute : HttpVerbAttribute
    {
        public Type Type { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpPostAttribute : HttpBodyAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpGetAttribute : HttpVerbAttribute
    {

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpPutAttribute : HttpBodyAttribute
    {

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpPatchAttribute : HttpBodyAttribute
    {

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpDeleteAttribute : HttpBodyAttribute
    {

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpOptionsAttribute : HttpVerbAttribute
    {

    }
}
