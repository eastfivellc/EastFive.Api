using EastFive.Api.Modules;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api.Core
{
    public class Middleware
    {
        private readonly RequestDelegate continueAsync;
        private readonly IApplication app;
        private ResourceInvocation[] resources;
        private static IDictionary<Type, MethodInfo[]> routeResourceExtensionLookup;

        private struct ResourceInvocation
        {
            public IInvokeResource invokeResourceAttr;
            public Type type;
        }

        public Middleware(RequestDelegate next, IApplication app)
        {
            this.continueAsync = next;
            this.app = app;
            LocateControllers();
        }

        public Task InvokeAsync(HttpContext context)
        {
            return resources
                .NullToEmpty()
                .Select(
                    resource =>
                    {
                        var doesHandleRequest = resource.invokeResourceAttr.DoesHandleRequest(
                            resource.type, context, out RouteData routeData);
                        return new
                        {
                            doesHandleRequest,
                            resource,
                            routeData,
                        };
                    })
                .Where(kvp => kvp.doesHandleRequest)
                .First(
                    async (requestHandler, next) =>
                    {
                        var resource = requestHandler.resource;
                        var httpRequestMessage = context.GetHttpRequestMessage();
                        var cancellationToken = new CancellationToken();
                        var extensionMethods = routeResourceExtensionLookup.ContainsKey(resource.type) ?
                            routeResourceExtensionLookup[resource.type]
                            :
                            new MethodInfo[] { };

                        var response = await app.GetType()
                            .GetAttributesInterface<IHandleRoutes>(true, true)
                            .Aggregate<IHandleRoutes, RouteHandlingDelegate>(
                                async (controllerTypeFinal, httpAppFinal, requestFinal, pathParameters, extensionMethodsFinal) =>
                                {
                                    var invokeResource = controllerTypeFinal.GetAttributesInterface<IInvokeResource>().First();
                                    var response = await invokeResource.CreateResponseAsync(controllerTypeFinal,
                                        httpAppFinal, requestFinal, cancellationToken,
                                        pathParameters, extensionMethodsFinal);
                                    return response;

                                    //return await resource.invokeResourceAttr.CreateResponseAsync(resource.type,
                                    //    this.app, httpRequestMessage, cancellationToken,
                                    //    requestHandler.routeData, extensionMethods);
                                },
                                (callback, routeHandler) =>
                                {
                                    return (controllerTypeCurrent, httpAppCurrent, requestCurrent, routeNameCurrent, pathParameters) =>
                                        routeHandler.HandleRouteAsync(controllerTypeCurrent,
                                            httpAppCurrent, requestCurrent, routeNameCurrent,
                                            pathParameters, callback);
                                })
                            .Invoke(resource.type, app, httpRequestMessage,
                                new RouteData(), extensionMethods);

                        
                        await response.WriteToContextAsync(context);
                    },
                    () => continueAsync(context));
        }

        private void LocateControllers()
        {
            object lookupLock = new object();
            
            var limitedAssemblyQuery = this.app.GetType()
                .GetAttributesInterface<IApiResources>(inherit: true, multiple: true);
            Func<Assembly, bool> shouldCheckAssembly =
                (assembly) =>
                {
                    return limitedAssemblyQuery
                        .First(
                            (limitedAssembly, next) =>
                            {
                                if (limitedAssembly.ShouldCheckAssembly(assembly))
                                    return true;
                                return next();
                            },
                            () => false);
                };

            AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
            {
                if (args.LoadedAssembly.GlobalAssemblyCache)
                    return;
                var check = shouldCheckAssembly(args.LoadedAssembly);
                if (!check)
                    return;
                lock (lookupLock)
                {
                    AddControllersFromAssembly(args.LoadedAssembly);
                }
            };

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => (!assembly.GlobalAssemblyCache))
                .Where(shouldCheckAssembly)
                .ToArray();

            lock (lookupLock)
            {
                foreach (var assembly in loadedAssemblies)
                {
                    AddControllersFromAssembly(assembly);
                }
            }
        }

        private void AddControllersFromAssembly(System.Reflection.Assembly assembly)
        {
            try
            {
                var types = assembly
                    .GetTypes();
                var functionViewControllerAttributesAndTypes = types
                    .Where(type => type.ContainsAttributeInterface<IInvokeResource>())
                    .Select(
                        (type) =>
                        {
                            var attr = type.GetAttributesInterface<IInvokeResource>().First();
                            return new ResourceInvocation
                            {
                                type = type,
                                invokeResourceAttr = attr,
                            };
                        });
                resources = resources
                    .NullToEmpty()
                    .Concat(functionViewControllerAttributesAndTypes)
                    .ToArray();

                var extendedMethods = types
                    .Where(type => type.ContainsAttributeInterface<IInvokeExtensions>())
                    .SelectMany(
                        (type) =>
                        {
                            var attr = type.GetAttributesInterface<IInvokeExtensions>().First();
                            return attr.GetResourcesExtended(type);
                        })
                    .ToArray();

                routeResourceExtensionLookup = extendedMethods
                    .ToDictionaryCollapsed((t1, t2) => t1.FullName == t2.FullName)
                    .Concat(routeResourceExtensionLookup.NullToEmpty().Where(kvp => !extendedMethods.Contains(kvp2 => kvp2.Key == kvp.Key)))
                    .ToDictionary();

            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                ex.GetType();
            }
            catch (Exception ex)
            {
                ex.GetType();
            }
        }

    }
}
