using BlackBarLabs.Api;
using BlackBarLabs.Api.Controllers;
using BlackBarLabs.Api.Resources;
using BlackBarLabs.Extensions;
using EastFive.Extensions;
using EastFive.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace EastFive.Api.Controllers
{
    public class BackgroundProgressQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        public IntQuery Top { get; set; }

        public IntQuery Count { get; set; }
    }

    public class BackgroundProgress : BlackBarLabs.Api.ResourceBase
    {
        [JsonProperty(PropertyName = "progress")]
        public double Progress { get; set; }

        [JsonProperty(PropertyName = "total")]
        public double Total { get; set; }

    }

    public class BackgroundProgressController : BaseController
    {
        public class Process
        {
            internal Guid id;
            internal HttpResponseMessage[] responses;
            internal double? length;
            internal Thread thread;
            internal double progress;
            internal HttpResponseMessage response;
        }

        private static ConcurrentDictionary<Guid, Process> processes = new ConcurrentDictionary<Guid, Process>();

        public IHttpActionResult Get([FromUri]BackgroundProgressQuery query)
        {
            return this.ActionResult(() =>query.ParseAsync(this.Request,
                (q) => GetProgressAsync(q.Id.ParamSingle(), this.Request),
                (q) => GetByRange(q.Id.ParamSingle(), q.Top.ParamMaybe(), q.Count.ParamValue(), this.Request),
                (q) => GetByRange(q.Id.ParamSingle(), q.Top.ParamValue(), q.Count.ParamMaybe(), this.Request)));
        }

        private Task<HttpResponseMessage> GetProgressAsync(Guid processId, HttpRequestMessage request)
        {
            if (!processes.TryGetValue(processId, out Process process))
                return request.CreateResponse(HttpStatusCode.NotFound).ToTask();
            var result =
                process.responses.IsDefaultOrNull()?
                    process.length.HasValue?
                        process.progress / process.length.Value
                        :
                        0.0
                :
                    process.length.HasValue ?
                        ((double)process.responses.Length) / ((double)process.length.Value)
                        :
                        0.0;

            return this.Request.CreateResponse(HttpStatusCode.OK,
                new BackgroundProgress
                {
                    Id = Url.GetWebId<BackgroundProgressController>(processId),
                    Progress = result,
                    Total = process.length.HasValue ? process.length.Value : 0.0,
                }).ToTask();
        }

        private async Task<HttpResponseMessage[]> GetByRange(Guid processId, int top, int? count, HttpRequestMessage request)
        {
            if (!processes.TryGetValue(processId, out Process process))
                return request.CreateResponseNotFound(processId).AsArray();

            var topValue = top;
            var countValue = count.HasValue ? count.Value : process.responses.Length - topValue;

            var results = await process.responses.Skip(topValue).Take(countValue).ToArray().ToTask();
            return results;
        }

        private async Task<HttpResponseMessage[]> GetByRange(Guid processId, int? top, int count, HttpRequestMessage request)
        {
            if (!processes.TryGetValue(processId, out Process process))
                return request.CreateResponseNotFound(processId).AsArray();

            var topValue = top.HasValue ? top.Value : 0;
            var countValue = count;

            var results = await process.responses.Skip(topValue).Take(countValue).ToArray().ToTask();
            return results;
        }

        internal static Guid CreateProcess(Func<Func<HttpResponseMessage, Process>, Task<Process[]>> callback, int? estimatedProcessLength)
        {
            var processId = Guid.NewGuid();
            var process = new Process
            {
                id = processId,
                responses = new HttpResponseMessage[] { },
                length = estimatedProcessLength.HasValue? (double)estimatedProcessLength.Value : default(double?),
            };
            if (callback != null)
            {
                process.thread = new Thread(
                    () =>
                    {
                        var task = new Task<Process[]>(
                            () =>
                            {
                                var completeTask = callback(
                                    (response) =>
                                    {
                                        lock (process)
                                        {
                                            process.responses = process.responses.Append(response).ToArray();
                                        }
                                        return process;
                                    });
                                return completeTask.Result;
                        });
                        task.RunSynchronously();
                    });
                process.thread.Start();
            }
            processes.AddOrUpdate(processId, process, (id, proc) => proc);
            return processId;
        }

        internal static Guid CreateProcess(
            Func<Action<double>, Task<HttpResponseMessage>> launched,
            double? estimatedProcessLength)
        {
            var processId = Guid.NewGuid();
            var process = new Process
            {
                id = processId,
                responses = default(HttpResponseMessage[]),
                response = default(HttpResponseMessage),
                length = estimatedProcessLength,
                progress = 0.0,
            };
            if (launched != null)
            {
                process.thread = new Thread(
                    () =>
                    {
                        var task = new Task<HttpResponseMessage>(
                            () =>
                            {
                                Action<double> callback = (progress) =>
                                {
                                    lock (process)
                                    {
                                        process.progress = progress;
                                    }
                                };
                                var completeTask = launched(callback);
                                return completeTask.Result;
                            });
                        task.RunSynchronously();
                    });
                process.thread.Start();
            }
            processes.AddOrUpdate(processId, process, (id, proc) => proc);
            return (processId);
        }
    }
}
