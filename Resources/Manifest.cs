using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Resources
{
    public class Manifest
    {
        public Manifest(KeyValuePair<string, KeyValuePair<HttpMethod, MethodInfo[]>[]>[] lookups)
        {
            this.Routes = lookups.Select(route => new Route(route.Key, route.Value)).ToArray();
        }
        public Route[] Routes { get; set; }

        public class Route
        {
            public Route(string name, KeyValuePair<HttpMethod, MethodInfo[]>[] methods)
            {
                this.Name = name;
                this.Verbs = methods.Select(verb => new Verb(verb.Key, verb.Value)).ToArray();
            }

            public string Name { get; set; }

            public Verb[] Verbs { get; set; }
        }

        public class Verb
        {
            public Verb(HttpMethod method, MethodInfo[] methods)
            {
                this.Method = method.Method;
                this.Methods = methods.Select(methodInfo => new Method(methodInfo)).ToArray();
            }

            public string Method { get; set; }

            public Method[] Methods { get; set; }
        }

        public class Method
        {
            public Method(MethodInfo methodInfo)
            {
                this.Name = methodInfo.Name;
                this.Parameters = methodInfo.GetParameters()
                    .Where(methodParam => methodParam.ContainsCustomAttribute<QueryValidationAttribute>(true))
                    .Select(paramInfo => new Parameter(paramInfo)).ToArray();
                this.Responses = methodInfo.GetParameters()
                    .Where(methodParam => typeof(MulticastDelegate).IsAssignableFrom(methodParam.ParameterType))
                    .Select(paramInfo => new Response(paramInfo)).ToArray();
            }

            public string Name { get; set; }

            public Parameter[] Parameters { get; set; }

            public Response[] Responses { get; set; }
        }

        public class Parameter
        {
            public Parameter(ParameterInfo paramInfo)
            {
                var validator = paramInfo.GetCustomAttribute<QueryValidationAttribute>();
                this.Name = validator.Name.IsNullOrWhiteSpace() ?
                    paramInfo.Name.ToLower()
                    :
                    validator.Name.ToLower();
                this.Required = !(paramInfo.ContainsCustomAttribute<PropertyOptionalAttribute>() ||
                    paramInfo.ContainsCustomAttribute<OptionalAttribute>());
                this.Default = paramInfo.ContainsCustomAttribute<QueryDefaultParameterAttribute>();
                this.Type = paramInfo.ParameterType;
            }

            public string Name { get; set; }
            public bool Required { get; set; }
            public bool Default { get; set; }

            public Type Type { get; set; }
        }

        public class Response
        {
            public Response(ParameterInfo paramInfo)
            {
                this.Name = paramInfo.Name;
                //this.Required = paramInfo.ContainsCustomAttribute<PropertyAttribute>() ||
                //    paramInfo.ContainsCustomAttribute<RequiredAttribute>();
                //this.Default = paramInfo.ContainsCustomAttribute<QueryDefaultParameterAttribute>();
                //this.Type = paramInfo.ParameterType;
                this.StatusCode = System.Net.HttpStatusCode.OK;
                this.Example = "TODO: JSON serialize response type";
                this.Headers = new KeyValuePair<string, string>[] { };
            }

            public string Name { get; set; }
            public System.Net.HttpStatusCode StatusCode { get; set; }
            public string Example { get; set; }

            public KeyValuePair<string, string>[] Headers { get; set; }
        }

    }
}
