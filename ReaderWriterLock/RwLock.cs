using System;
using System.Threading;

namespace ReaderWriterLock
{
    public class RwLock : IRwLock
    {
        private readonly object lockObject = new();
        private ulong currentReadersCount;
        private readonly EventWaitHandle waitHandle = new AutoResetEvent(false);
        
        public void ReadLocked(Action action)
        {
            lock (lockObject)
            {
                Interlocked.Increment(ref currentReadersCount);
            }

            try
            {
                action();
            }
            finally
            {
                if (Interlocked.Decrement(ref currentReadersCount) == 0) 
                    waitHandle.Set();
            }
        }

        public void WriteLocked(Action action)
        {
            lock (lockObject)
            {
                while (Interlocked.Read(ref currentReadersCount) != 0) 
                    waitHandle.WaitOne();
                action();
            }
        }
    }
}