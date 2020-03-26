using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using Microsoft.AspNetCore.Http;

namespace EastFive.Api
{
    public class FunctionViewController6Attribute : FunctionViewController5Attribute
    {
        protected override IEnumerable<MethodInfo> GetHttpMethods(Type controllerType,
            IApplication httpApp, IHttpRequest routeData)
        {
            var matchingActionMethods = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(httpApp.GetExtensionMethods(controllerType))
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .Where(
                    method =>
                    {
                        var routeMatcher = method.GetAttributesInterface<IMatchRoute>().Single();
                        return routeMatcher.IsMethodMatch(method, routeData, httpApp);
                    });
            return matchingActionMethods;
        }

        public override Route GetRoute(Type type, HttpApplication httpApp)
        {
            var actionMethods = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(httpApp.GetExtensionMethods(type))
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .ToArray();

            return new Route(type, this.Route, 
                actionMethods,
                type.GetMembers(BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance),
                httpApp);
        }

        public override bool DoesHandleRequest(Type type, IHttpRequest request)
        {
            if (this.Namespace.HasBlackSpace())
            {
                if (!DoesMatch(0, this.Namespace))
                    return false;
            }

            if (this.Route.HasBlackSpace())
            {
                var doesMatch = DoesMatch(1, this.Route);
                return doesMatch;
            }

            return true;

            bool DoesMatch(int index, string value)
            {
                var requestUrl = request.GetAbsoluteUri();
                var path = requestUrl.AbsolutePath;
                var pathParameters = path
                    .Split('/'.AsArray())
                    .Where(v => v.HasBlackSpace())
                    .ToArray();
                if (pathParameters.Length <= index)
                    return false;
                var component = pathParameters[index];
                if (!component.Equals(value, StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;
            }
        }
    }
}
