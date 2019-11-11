using EastFive.Api.Bindings;
using EastFive.Api.Serialization;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpActionAttribute : HttpVerbAttribute
    {
        public HttpActionAttribute(string method)
        {
            this.Action = method;
        }

        public string Action { get; set; }

        public override string Method => Action;

        public override bool IsMethodMatch(MethodInfo method, HttpRequestMessage request, IApplication httpApp)
        {
            var path = request.RequestUri.Segments
                .Skip(1)
                .Select(segment => segment.Trim('/'.AsArray()))
                .Where(pathPart => !pathPart.IsNullOrWhiteSpace())
                .ToArray();
            if (path.Length < 3)
                return false;
            var action = path.Skip(2).First();

            var isMethodMatch = String.Compare(Action, action, true) == 0;
            return isMethodMatch;
        }

        protected override CastDelegate GetFileNameCastDelegate(
            HttpRequestMessage request, IApplication httpApp, out string[] pathKeys)
        {
            var path = request.RequestUri.Segments
                .Skip(1)
                .Select(segment => segment.Trim('/'.AsArray()))
                .Where(pathPart => !pathPart.IsNullOrWhiteSpace())
                .ToArray();
            pathKeys = path.Skip(3).ToArray();
            CastDelegate fileNameCastDelegate =
                (paramInfo, onParsed, onFailure) =>
                {
                    if (path.Length < 4)
                        return onFailure("No URI filename value provided.");
                    return httpApp.Bind(path[3], paramInfo,
                            v => onParsed(v),
                            (why) => onFailure(why));
                };
            return fileNameCastDelegate;
        }
    }
}
