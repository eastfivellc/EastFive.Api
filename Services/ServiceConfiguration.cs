using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BlackBarLabs.Linq;
using EastFive.Web.Services;
using EastFive.Api.Services;

namespace EastFive.Api.Services
{
    public static class ServiceConfiguration
    {
        public static void Initialize(System.Web.Http.HttpConfiguration config,
            Func<ISendMessageService> sendMessageService,
            Func<ITimeService> timeService)
        {
            config.MessageHandlers.Add(new Modules.ControllerModule(config));
            EastFive.Web.Services.ServiceConfiguration.Initialize(sendMessageService, timeService);
        }

        private static void SetupDemandLoading(System.Web.Http.HttpConfiguration config)
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => (!assembly.GlobalAssemblyCache))
                .ToArray();

            AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
            {
                InitializeTypes(args.LoadedAssembly, config,
                    () => true,
                    () => false,
                    (why) => false);
            };

            loadedAssemblies.Select(
                assembly => InitializeTypes(assembly, config,
                () => true,
                () => false,
                (why) => false));
        }

        private static TResult InitializeTypes<TResult>(System.Reflection.Assembly assembly,
                System.Web.Http.HttpConfiguration config,
            Func<TResult> onSuccess,
            Func<TResult> onUninitialized,
            Func<string, TResult> onFailure)
        {
            var types = assembly
                .GetTypes();
            var results = types
                .Where(type => type.IsClass && type.IsAssignableFrom(typeof(EastFive.Api.Services.IIdentityService)))
                .Select(
                    //new
                    //{
                    //    loaded = new Type[] { },
                    //    skipped = new Type[] { },
                    //    failed = new Type[] { },
                    //},
                    (type) =>
                    {
                        var instance = (Services.IIdentityService)Activator.CreateInstance(type);
                        return instance;
                        //return instance.Initialize(config, serviceConfiguration,
                        //    () => new
                        //    {
                        //        loaded = aggr.loaded.Append(type),
                        //        skipped = aggr.skipped,
                        //        failed = aggr.failed,
                        //    },
                        //    () => new
                        //    {
                        //        loaded = aggr.loaded,
                        //        skipped = aggr.skipped.Append(type),
                        //        failed = aggr.failed,
                        //    },
                        //    (why) => new
                        //    {
                        //        loaded = aggr.loaded,
                        //        skipped = aggr.skipped,
                        //        failed = aggr.failed.Append(type),
                        //    });
                    })
                .ToArray();

            return onSuccess();
        }
    }
}
