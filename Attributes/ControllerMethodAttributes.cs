using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public abstract class HttpVerbAttribute : Attribute
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

        public abstract string Method { get; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public abstract class HttpBodyAttribute : HttpVerbAttribute
    {
        public Type Type { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpPostAttribute : HttpBodyAttribute
    {
        public override string Method => System.Net.Http.HttpMethod.Post.Method;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpGetAttribute : HttpVerbAttribute
    {
        public override string Method => System.Net.Http.HttpMethod.Get.Method;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpActionAttribute : HttpVerbAttribute
    {
        public HttpActionAttribute(string method)
        {
            this.Action = method;
        }

        public string Action { get; set; }

        public override string Method => Action;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpPutAttribute : HttpBodyAttribute
    {
        public override string Method => System.Net.Http.HttpMethod.Put.Method;

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpPatchAttribute : HttpBodyAttribute
    {
        public override string Method => "Patch";

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpDeleteAttribute : HttpBodyAttribute
    {
        public override string Method => System.Net.Http.HttpMethod.Delete.Method;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpOptionsAttribute : HttpVerbAttribute
    {
        public override string Method => System.Net.Http.HttpMethod.Options.Method;
    }
}
