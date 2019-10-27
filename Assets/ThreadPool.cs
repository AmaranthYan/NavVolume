using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NavVolume.Utility
{
    public class ThreadPool : IDisposable
    {
        public delegate void ThreadTask();

        private ConcurrentQueue<ThreadTask> m_TaskQueue = new ConcurrentQueue<ThreadTask>();
        private int m_Disposed = 0;

        private Thread[] m_WorkerThread;
        private object m_PoolLock = new object();
        private int m_AvailableThreadCount = 0;

        private void ThreadJob()
        {
            SpinWait spinWait = new SpinWait();
            while (true)
            {
                ThreadTask currentTask = null;
                if (m_TaskQueue.TryDequeue(out currentTask))
                {
                    Interlocked.Decrement(ref m_AvailableThreadCount);
                    currentTask.Invoke();
                    Interlocked.Increment(ref m_AvailableThreadCount);
                }
                else if (m_Disposed != 0)
                {
                    break;
                }
                else
                {
                    spinWait.SpinOnce();
                }
            }
        }

        public ThreadPool(int threadCount)
        {
            m_WorkerThread = new Thread[threadCount];
            Interlocked.Exchange(ref m_AvailableThreadCount, threadCount);
            for (int i = 0; i < threadCount; i++)
            {
                m_WorkerThread[i] = new Thread(new ThreadStart(ThreadJob));
                m_WorkerThread[i].Start();
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref m_Disposed, 1, 0) == 0)
            {
                foreach (var thread in m_WorkerThread)
                {
                    if (thread != null)
                    {
                        thread.Join();
                    }
                }
            }
        }

        public int AvailableThreadCount
        {
            get
            {
                return m_Disposed == 0 ? m_AvailableThreadCount : 0;
            }
        }

        public void QueueTask(ThreadTask task)
        {
            if (m_Disposed != 0) throw new ObjectDisposedException("The thread pool instance has been disposed");

            m_TaskQueue.Enqueue(task);
        }
    }
}