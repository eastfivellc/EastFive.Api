using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using EastFive.Serialization;
using System.Net.NetworkInformation;
using EastFive.Extensions;
using BlackBarLabs.Web;
using System.Reflection;
using System.Net.Http;
using EastFive.Linq;
using System.Net;

namespace EastFive.FVC.Modules
{
    public class ControllerModule : IHttpModule
    {
        private IDictionary<string, IDictionary<HttpMethod, MethodInfo[]>> lookup;
        private object lookupLock = new object();

        public ControllerModule()
        {
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public void Init(HttpApplication context)
        {
            var wrapper = new EventHandlerTaskAsyncHelper(RouteToControllerAsync);
            context.AddOnBeginRequestAsync(wrapper.BeginEventHandler, wrapper.EndEventHandler);

        }

        private async Task RouteToControllerAsync(object sender, EventArgs e)
        {
            var httpApp = (HttpApplication)sender;
            var context = httpApp.Context;
            string filePath = context.Request.FilePath;
            string fileName = VirtualPathUtility.GetFileName(filePath);

            if (lookup.IsDefault())
                LocateControllers();

            var routeName = fileName.IsNullOrWhiteSpace().IfElse(
                () =>
                {
                    var path = filePath.Split(new char[] { '/' });
                    return path.Any() ? path.Last() : "";
                },
                () => fileName);

            if (!lookup.ContainsKey(routeName))
                return;

            var possibleHttpMethods = lookup[routeName];
            var matchingKey = possibleHttpMethods
                .SelectKeys()
                .Where(key => key.Method == context.Request.HttpMethod);

            if (!matchingKey.Any())
                context.Response.StatusCode = (int)HttpStatusCode.NotImplemented;

            if (HttpContext.Current.Items.Contains("MS_HttpRequestMessage"))
                return;

            var httpRequestMessage = HttpContext.Current.Items["MS_HttpRequestMessage"] as HttpRequestMessage;
            await CreateResponseAsync(httpRequestMessage, possibleHttpMethods[matchingKey.First()]);
            
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
        
        private void LocateControllers()
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => (!assembly.GlobalAssemblyCache))
                .ToArray();

            var lookupLock = new Object();
            lock (lookupLock)
            {
                AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
                {
                    lock (lookupLock)
                    {
                        AddControllersFromAssembly(args.LoadedAssembly);
                    }
                };

                foreach (var assembly in loadedAssemblies)
                {
                    AddControllersFromAssembly(assembly);
                }
            }
        }

        IDictionary<Type, HttpMethod> methodLookup =
            new Dictionary<Type, HttpMethod>()
            {
                { typeof(EastFive.Api.HttpGetAttribute), HttpMethod.Get },
                { typeof(EastFive.Api.HttpDeleteAttribute), HttpMethod.Delete },
                { typeof(EastFive.Api.HttpPostAttribute), HttpMethod.Post },
                { typeof(EastFive.Api.HttpPutAttribute), HttpMethod.Put },
                { typeof(EastFive.Api.HttpOptionsAttribute), HttpMethod.Options },
            };

        private void AddControllersFromAssembly(System.Reflection.Assembly assembly)
        {
            var types = assembly
                .GetTypes();
            var results = types
                .Where(type => type.IsClass && type.ContainsCustomAttribute<FunctionViewControllerAttribute>())
                .Select(
                    (type) =>
                    {
                        var attr = type.GetCustomAttribute<FunctionViewControllerAttribute>();
                        IDictionary<HttpMethod, MethodInfo[]> methods = methodLookup
                            .Select(
                                methodKvp => methodKvp.Value.PairWithValue(
                                    type
                                        .GetMethods()
                                        .Where(method => method.ContainsCustomAttribute(methodKvp.Key))
                                .ToArray()))
                            .ToDictionary();
                        return attr.Route.PairWithValue(methods);
                    })
                .ToArray();

            this.lookup = this.lookup.Concat(results).ToDictionary();
        }

        private async Task CreateResponseAsync(HttpRequestMessage request, MethodInfo [] methods)
        {
            var queryParams = request.RequestUri.ParseQuery();

            //var responseMessage = methods
            //    .Aggregate(
            //        (method, nextMethod, skipMethod) =>
            //        {
            //            var parameters = method.GetParameters();
            //            var unvalidatedProperties = parameters
            //                .Where(param => param.ContainsCustomAttribute<QueryValidationAttribute>());
            //            return unvalidatedProperties;
            //            //    .Aggregate(
            //            //        queryParams,
            //            //        (props) =>
            //            //        {
            //            //            return methodCallExpression.Method.GetCustomAttribute(
            //            //                (ApiValidations.ValidationAttribute validationAttr) =>
            //            //                {
            //            //                    // TODO: Catch convert here for Casted parameters (defined -> nullable, etc)
            //            //                    var memberLookupArgumentsMatchingResourceType = methodCallExpression.Arguments
            //            //                        .Where(arg => arg is MemberExpression)
            //            //                        .Where(arg => (arg as MemberExpression).Member is MemberInfo)
            //            //                        .Where(arg => ((arg as MemberExpression).Member as MemberInfo).ReflectedType.IsAssignableFrom(typeof(TQuery)));
            //            //                    if (!memberLookupArgumentsMatchingResourceType.Any())
            //            //                        return props;
            //            //                    var memberLookupArgument = memberLookupArgumentsMatchingResourceType.First();
            //            //                    var member = ((memberLookupArgument as MemberExpression).Member as MemberInfo);
            //            //                    var v = member.GetValue(resource);
            //            //                    var conversionMethod = methodCallExpression.Method;
            //            //                    var validationFunction = paramFunctions[memberLookupArgument.Type][conversionMethod.ReturnType];
            //            //                    var validationResult = validationFunction(v, this);
            //            //                    if (validationResult.GetType() != validationAttr.GetType())
            //            //                        return props.Append(member).ToArray();

            //            //                    var reducedProps = props
            //            //                        .Where(prop => prop.Name != member.Name)
            //            //                        .ToArray();
            //            //                    return reducedProps;
            //            //                },
            //            //                () => props);
            //            //        });

            //            //if (unvalidatedProperties.Any())
            //            //    return nextExpression(unvalidatedProperties.ToArray());

            //            //return parameters
            //            //    .SelectReduce(
            //            //        (param, next) =>
            //            //        {
            //            //            if (param.Type == typeof(TQuery))
            //            //                return next(resource);
            //            //            if (instigators.ContainsKey(param.Type))
            //            //                return instigators[param.Type](this,
            //            //                    (v) => next(v));
            //            //            if (optionalGenerators.ContainsKey(param.Type))
            //            //            {
            //            //                return optionalGenerators[param.Type](this,
            //            //                        generatedValue => next(generatedValue),
            //            //                        () => nextExpression(unvalidatedProperties.ToArray()));
            //            //            }
            //            //            return Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
            //            //                .AddReason($"Could not instatiate type: {param.Type.FullName}")
            //            //                .ToTask();
            //            //        },
            //            //        async (object[] paramValues) =>
            //            //        {
            //            //            // Expression.Call()
            //            //            // lambdaExpr.CompileToMethod()
            //            //            HttpResponseMessage response = (doubleAwait) ?
            //            //                await await (Task<Task<HttpResponseMessage>>)lambdaExpr.Compile().DynamicInvoke(paramValues)
            //            //                :
            //            //                await (Task<HttpResponseMessage>)lambdaExpr.Compile().DynamicInvoke(paramValues);
            //            //            return response;
            //            //        });
            //        },
            //        (unvalidateds) =>
            //        {
            //            var content = $"Please include a value for one of [{unvalidateds.Select(uvs => uvs.Select(uv => uv.Name).Join(",")).Join(" or ")}]";
            //            return Request
            //                .CreateResponse(System.Net.HttpStatusCode.NotImplemented)
            //                .AddReason(content)
            //                .ToTask();
            //        });

            //return responseMessage;
        }
    }
}
