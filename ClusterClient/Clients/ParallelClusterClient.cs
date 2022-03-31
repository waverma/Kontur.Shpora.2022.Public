using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClusterClient.Clients;
using log4net;

namespace ClusterTests
{
    public class ParallelClusterClient : ClusterClientBase
    {
        public ParallelClusterClient(string[] replicaAddresses)
            : base(replicaAddresses)
        {
        }

        public override async Task<string> ProcessRequestAsync(string query, TimeSpan timeout)
        {
            var tasks = new List<Task<string>>();
            
            foreach (var uri in ReplicaAddresses)
            {
                var webRequest = CreateRequest(uri + "?query=" + query);
            
                Log.InfoFormat($"Processing {webRequest.RequestUri}");

                tasks.Add(ProcessRequestAsync(webRequest));
            }

            do
            {
                await WhenAny(tasks, Task.Delay(timeout));

                var completedTask = tasks.FirstOrDefault(x => x.IsCompleted);
                if (completedTask is null) break;
                if (!completedTask.IsFaulted) 
                    return completedTask.Result;
                tasks.RemoveAll(x => x.IsFaulted);
            } while (tasks.Any());
            
            throw new TimeoutException();
        }

        protected override ILog Log => LogManager.GetLogger(typeof(ParallelClusterClient));
    }
}