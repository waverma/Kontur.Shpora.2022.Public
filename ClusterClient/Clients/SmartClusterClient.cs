#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ClusterClient.Clients;
using log4net;

namespace ClusterTests
{
    public class SmartClusterClient : ClusterClientBase
    {
        public SmartClusterClient(string[] replicaAddresses)
            : base(replicaAddresses)
        {
            lock (Statistics)
            {
                foreach (var replicaAddress in replicaAddresses)
                {
                    if (!Statistics.ContainsKey(replicaAddress))
                        Statistics[replicaAddress] = new ReplicaStatistic(replicaAddress);
                }
            }
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var relevant = TryGetUrisOrderedByResponseSpeedIfStatisticRecent(out var ordered);
            
            var replicasCountToGo = ordered.Length;
            var stopwatch = new Stopwatch();
            var tasks = new List<Task<string>>();

            foreach (var uri in ordered)
            {
                var timeoutByReplica = timeout / replicasCountToGo;
                var webRequest = CreateRequest(uri + "?query=" + query);
            
                Log.InfoFormat($"Processing {webRequest.RequestUri}");

                var task = relevant 
                    ? ProcessRequestAsync(webRequest) 
                    : Calculate(webRequest, uri);
                tasks.Add(task);
                tasks.RemoveAll(x => x.IsFaulted);
                
                stopwatch.Restart();
                await WhenAny(tasks, Task.Delay(timeoutByReplica));
                stopwatch.Stop();
                
                task = tasks.FirstOrDefault(x => x.IsCompleted && !x.IsFaulted);
                if (task is not null)
                {
                    return task.Result;
                }
                
                timeout -= new TimeSpan(stopwatch.ElapsedTicks);
                replicasCountToGo--;
            }
            
            throw new TimeoutException();
        }

        private static readonly Dictionary<string, ReplicaStatistic> Statistics = new();
        private Task<string> Calculate(HttpWebRequest? request, string uri)
        {
            var stopwatch = new Stopwatch();
            
            return Task.Run(async () =>
            {
                stopwatch.Start();
                var result = await ProcessRequestAsync(request);
                stopwatch.Stop();

                lock (Statistics)
                {
                    Statistics[uri].ResetResponseTime(stopwatch.ElapsedTicks);
                }
                
                return result;
            });
        }

        private bool TryGetUrisOrderedByResponseSpeedIfStatisticRecent(out string[] result)
        {
            result = ReplicaAddresses;
            
            lock (Statistics)
            {
                if (Statistics.Any(x => !x.Value.IsStatisticRecent() && ReplicaAddresses.Contains(x.Key)))
                    return false;

                result = Statistics.OrderBy(x => x.Value.ResponseTime).Select(x => x.Key).Where(x => ReplicaAddresses.Contains(x)).ToArray();
            }

            return true;
        }

        protected override ILog Log => LogManager.GetLogger(typeof(SmartClusterClient));
    }
}