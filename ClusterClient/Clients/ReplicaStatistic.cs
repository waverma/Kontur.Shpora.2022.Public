using System;

namespace ClusterTests
{
    public class ReplicaStatistic
    {
        private const long TtlInSeconds = 2;

        public long ResponseTime { get; private set; }
        private DateTime responseSettingDate;

        public ReplicaStatistic(string uri)
        {
            ResponseTime = 0;
        }

        public void ResetResponseTime(long ticks)
        {
            ResponseTime = ticks;
            responseSettingDate = DateTime.Now;
        }

        public bool IsStatisticRecent()
        {
            return responseSettingDate + TimeSpan.FromSeconds(TtlInSeconds) > DateTime.Now;
        }
    }
}