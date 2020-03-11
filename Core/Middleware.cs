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
                .First(
                    async (resource, next) =>
                    {
                        if (!resource.invokeResourceAttr.DoesHandleRequest())
                        {
                            await next();
                            return;
                        }

                        var httpRequestMessage = context.GetHttpRequestMessage();
                        var cancellationToken = new CancellationToken();
                        var response = await resource.invokeResourceAttr.CreateResponseAsync(resource.type, this.app,
                            httpRequestMessage, cancellationToken, "");
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
