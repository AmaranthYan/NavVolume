using System;
using System.Collections.Generic;
using System.Threading;

namespace NavVolume.Utility
{
    public class ThreadPool : IDisposable
    {
        public delegate void ThreadTask();

        private Queue<ThreadTask> m_TaskQueue = new Queue<ThreadTask>();
        private bool m_Disposed = false;

        private Thread[] m_WorkerThread;
        private object m_PoolLock = new object();
        private int m_AvailableThreadCount = 0;

        private void ThreadJob()
        {
            while (true)
            {
                ThreadTask currentTask = null;
                lock (m_PoolLock)
                {
                    if (m_TaskQueue.Count > 0)
                    {
                        currentTask = m_TaskQueue.Dequeue();
                    }
                    else if (m_Disposed)
                    {
                        break;
                    }
                    else
                    {
                        m_AvailableThreadCount++;
                        Monitor.Wait(m_PoolLock);
                        m_AvailableThreadCount--;
                    }
                }

                // execute thread task
                currentTask?.Invoke();
            }
        }

        public ThreadPool(int threadCount)
        {
            m_WorkerThread = new Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                m_WorkerThread[i] = new Thread(new ThreadStart(ThreadJob));
                m_WorkerThread[i].Start();
            }
        }

        public void Dispose()
        {
            bool disposing = false;

            lock (m_PoolLock)
            {
                if (!m_Disposed)
                {
                    m_Disposed = true;
                    Monitor.PulseAll(m_PoolLock);
                    disposing = true;
                }
            }

            if (disposing)
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
                lock (m_PoolLock)
                {
                    return m_Disposed ? 0 : m_AvailableThreadCount;
                }
            }
        }

        public void QueueTask(ThreadTask task)
        {
            lock (m_PoolLock)
            {
                if (m_Disposed) throw new ObjectDisposedException("The thread pool instance has been disposed");

                m_TaskQueue.Enqueue(task);
                Monitor.Pulse(m_PoolLock);
            }
        }
    }
}