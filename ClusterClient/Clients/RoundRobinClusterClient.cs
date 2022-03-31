using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ClusterClient.Clients;
using log4net;

namespace ClusterTests
{
    public class RoundRobinClusterClient : ClusterClientBase
    {
        public RoundRobinClusterClient(string[] replicaAddresses)
            : base(replicaAddresses)
        {
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var replicasCountToGo = ReplicaAddresses.Length;
            var stopwatch = new Stopwatch();

            foreach (var uri in ReplicaAddresses)
            {
                var timeoutByReplica = timeout / replicasCountToGo;
                var webRequest = CreateRequest(uri + "?query=" + query);
            
                Log.InfoFormat($"Processing {webRequest.RequestUri}");

                stopwatch.Restart();
                var resultTask = ProcessRequestAsync(webRequest);
                await Task.WhenAny(resultTask, Task.Delay(timeoutByReplica));
                stopwatch.Stop();
                
                if (resultTask.IsCompleted && !resultTask.IsFaulted)
                    return resultTask.Result;
                
                timeout -= new TimeSpan(stopwatch.ElapsedTicks);
                replicasCountToGo--;
            }

            throw new TimeoutException();
        }

        protected override ILog Log => LogManager.GetLogger(typeof(RoundRobinClusterClient));
    }
}