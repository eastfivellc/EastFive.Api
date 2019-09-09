using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Reflection;
using EastFive.Extensions;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;

namespace EastFive.Api
{
    public class FunctionViewControllerExAttribute : Attribute, IInvokeExtensions
    {
        public KeyValuePair<Type, MethodInfo>[] GetResourcesExtended(Type extensionType)
        {
            return extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(method => method.IsExtension())
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .Select(method => method.PairWithKey(method.GetParameters().First().ParameterType))
                .ToArray();
        }
    }
}
