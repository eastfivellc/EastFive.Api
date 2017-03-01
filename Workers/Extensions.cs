using BlackBarLabs.Api.Workers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace BlackBarLabs.Api
{
    public static class WorkerExtensions
    {
        public static string RegisterBackgroundTask(this HttpApplicationState application, Action backgroundTask)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += new BackgroundWorker.DoWorkEventHandler(ListenerCallback);
            worker.RunWorker(backgroundTask);
            string taskName = Guid.NewGuid().ToString("N");
            application[taskName] = worker;
            return taskName;
        }
        
        /// <summary> 
        /// This operation listens to the queues without end
        /// </summary> 
        private static void ListenerCallback(ref int progress,
            ref object _result, params object[] arguments)
        {
            var processor = (Action)arguments[0];
            // Do the operation every 1 second wihout the end. 
            while (true)
            {
                processor();
            }
        }
    }
}
