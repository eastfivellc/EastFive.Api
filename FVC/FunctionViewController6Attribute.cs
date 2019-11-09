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

namespace EastFive.Api
{
    public class FunctionViewController6Attribute : FunctionViewController5Attribute
    {
        protected override IEnumerable<MethodInfo> GetHttpMethods(Type controllerType,
            IApplication httpApp, HttpRequestMessage request)
        {
            var matchingActionMethods = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(httpApp.GetExtensionMethods(controllerType))
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .Where(
                    method =>
                    {
                        var routeMatcher = method.GetAttributesInterface<IMatchRoute>().Single();
                        return routeMatcher.IsMethodMatch(method, request, httpApp);
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

            return new Route(this.Route, 
                actionMethods,
                type.GetMembers(BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance),
                httpApp);
        }

    }
}
