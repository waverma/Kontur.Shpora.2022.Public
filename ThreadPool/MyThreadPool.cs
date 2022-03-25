using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ThreadPool;

namespace ThreadPool
{
    internal class MyThreadPool : IThreadPool
    {
        private readonly Thread[] pool;
        private bool stopFlag;

        private readonly ConcurrentQueue<Action> actionsQueue;

        public MyThreadPool(int concurrency)
        {
            pool = new Thread[concurrency];
            actionsQueue = new ConcurrentQueue<Action>();

            for (int i = 0; i < concurrency; i++)
            {
                var thread = new Thread(() =>
                {
                    var action = default(Action);
                    while (!stopFlag)
                    {
                        Monitor.Enter(actionsQueue);
                        while (!actionsQueue.TryDequeue(out action))
                        {
                            if (actionsQueue.Count == 0)
                            {
                                Monitor.Wait(actionsQueue);
                            }
                            if (stopFlag) return;
                        }
                        Monitor.Exit(actionsQueue);
                        action();
                    }
                }){IsBackground = true};

                thread.Start();
                pool[i] = thread;
            }
        }

        private readonly object lockObj = new();
        public void Dispose()
        {
            lock (lockObj)
            {
                stopFlag = true;
            }
            actionsQueue.Clear();
            foreach (var thread in pool)
            {
                thread.Join();
            }
        }
        public void EnqueueAction(Action action)
        {
            actionsQueue.Enqueue(action);
            Monitor.Pulse(actionsQueue);
        }
    }
}