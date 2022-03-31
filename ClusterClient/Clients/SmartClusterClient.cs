#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ClusterClient.Clients;
using log4net;

namespace ClusterTests
{
    public class SmartClusterClient : ClusterClientBase
    {
        private IEnumerable<string> orderedReplicaAddresses;
        private readonly object lockObject = new();

        public SmartClusterClient(string[] replicaAddresses)
            : base(replicaAddresses)
        {
            orderedReplicaAddresses = replicaAddresses;
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            string[] replicasAddresses;
            lock (lockObject)
            {
                replicasAddresses = orderedReplicaAddresses.ToArray();
            }
            
            var replicasCountToGo = replicasAddresses.Length;
            var stopwatch = new Stopwatch();
            var tasks = new List<Task<string>>();

            foreach (var uri in replicasAddresses)
            {
                var timeoutByReplica = timeout / replicasCountToGo;
                var webRequest = CreateRequest(uri + "?query=" + query);
            
                Log.InfoFormat($"Processing {webRequest.RequestUri}");

                var resultTask = ProcessRequestAsync(webRequest);
                tasks.Add(resultTask);
                var faultedTasks = tasks.Where(x => x.IsFaulted);
                tasks.RemoveAll(x => x.IsFaulted);
                stopwatch.Restart();
                await WhenAny(tasks, Task.Delay(timeoutByReplica));
                stopwatch.Stop();
                
                var task = tasks.FirstOrDefault(x => x.IsCompleted && !x.IsFaulted);
                if (task is not null)
                    return task.Result;
                
                timeout -= new TimeSpan(stopwatch.ElapsedTicks);
                replicasCountToGo--;
            }
            throw new TimeoutException();
        }

        protected override ILog Log => LogManager.GetLogger(typeof(SmartClusterClient));
    }
}