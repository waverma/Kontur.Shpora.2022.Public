using System;
using System.Threading;

namespace ReaderWriterLock
{
    // public class OldRwLock : IRwLock
    // {
    //     private bool writing;
    //     private readonly object locker = new();
    //     private long currentReadersCount;
    //     
    //     public void ReadLocked(Action action)
    //     {
    //         Interlocked.Increment(ref currentReadersCount);
    //         while (writing)
    //         {
    //             Interlocked.Decrement(ref currentReadersCount);
    //             Thread.Yield();
    //             Interlocked.Increment(ref currentReadersCount);
    //         }
    //
    //         action();
    //         Interlocked.Decrement(ref currentReadersCount);
    //     }
    //
    //     public void WriteLocked(Action action)
    //     {
    //         lock (locker)
    //         {
    //             writing = true;
    //
    //             while (Interlocked.Read(ref currentReadersCount) != 0) Thread.Yield();
    //             action();
    //
    //             writing = false;
    //         }
    //     }
    // }

    public class RwLock : IRwLock
    {
        private readonly object lockObject = new();
        private long currentReadersCount;
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
                Interlocked.Decrement(ref currentReadersCount);
            }
            
            if (Interlocked.Read(ref currentReadersCount) == 0) 
                waitHandle.Set();
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