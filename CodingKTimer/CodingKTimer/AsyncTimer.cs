using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodingKTimer
{
    /// <summary>
    /// 使用async await语法驱动,运行在线程池中,可设置 多线程 或 逻辑主线程 来执行回调任务
    /// </summary>
    public class AsyncTimer : CodingKTimer
    {
        private const string tidLock = "AsyncTimer_tidLock";
        private readonly ConcurrentDictionary<int, AsyncTask> taskDic;
        private readonly ConcurrentQueue<AsyncTaskPack> packQue;
        private readonly bool setHandle;


        public AsyncTimer(bool setHandle)
        {
            taskDic = new ConcurrentDictionary<int, AsyncTask>();
            this.setHandle = setHandle;

            if (setHandle)
            {
                packQue = new ConcurrentQueue<AsyncTaskPack>();
            }
        }

        protected override int GenerateTid()
        {
            lock (tidLock)
            {
                while (true)
                {
                    ++m_tid;
                    if (m_tid == Int32.MaxValue)
                    {
                        m_tid = 0;
                    }

                    if (!taskDic.ContainsKey(m_tid))
                    {
                        return m_tid;
                    }
                }
            }
        }

        public override int AddTask(uint firstDelay, Action<int> taskCB, Action<int> cancelCB, uint delay = 0, int count = 1)
        {
            int tid = GenerateTid();
            AsyncTask task = new AsyncTask(tid, firstDelay, delay, count, taskCB, cancelCB);
            RunTaskInPool(task);

            if (taskDic.TryAdd(tid, task))
            {
                return tid;
            }
            else
            {
                WarnFunc?.Invoke($"KEY:{tid} already exist.");
                return -1;
            }
        }

        public override bool DeleteTask(int tid)
        {
            if (taskDic.TryRemove(tid, out AsyncTask task))
            {
                LogFunc?.Invoke($"Remove tid:{task.tid} task in taskDic success.");
                task.cts.Cancel();

                if (setHandle && task.cancelCB != null)
                {
                    packQue.Enqueue(new AsyncTaskPack(task.tid, task.cancelCB));
                }
                else
                {
                    task.cancelCB?.Invoke(task.tid);
                }

                return true;
            }
            else
            {
                WarnFunc?.Invoke($"KEY:{tid} remove failed when custom delete.");
                return false;
            }
        }

        public override void Reset()
        {
            if (packQue != null && !packQue.IsEmpty)
            {
                WarnFunc?.Invoke("Reset:packQue is not empty.");
            }

            taskDic.Clear();
        }

        private void RunTaskInPool(AsyncTask task)
        {
            Task.Run(async () =>
            {
                if (task.count > 0)
                {
                    do
                    {
                        // 限次循环任务

                        bool isFirstTime = false;
                        int firstDelay = 0;
                        if (task.firstDelay > 0)
                        {
                            firstDelay = (int)task.firstDelay;
                            task.firstDelay = 0;
                            isFirstTime = true;
                        }

                        --task.count;

                        int delay = isFirstTime ? firstDelay : (int)(task.delay + task.fixDelta);
                        if (delay > 0)
                        {
                            await Task.Delay(delay, task.ct);
                        }

                        if (isFirstTime)
                        {
                            task.startTime = task.startTime.AddMilliseconds(firstDelay);
                        }
                        else
                        {
                            ++task.loopIndex;

                            // 计算出实际开销时间
                            TimeSpan ts = DateTime.UtcNow - task.startTime;
                            // 修正实际时间值 = 理论开销时间 - 实际开销时间
                            task.fixDelta = (int)(task.delay * task.loopIndex - ts.TotalMilliseconds);
                        }

                        CallBackTaskCB(task);
                    } while (task.count > 0);
                }
                else
                {
                    // 永久循环任务
                    while (true)
                    {
                        bool isFirstTime = false;
                        int firstDelay = 0;
                        if (task.firstDelay > 0)
                        {
                            firstDelay = (int)task.firstDelay;
                            task.firstDelay = 0;
                            isFirstTime = true;
                        }

                        int delay = isFirstTime ? firstDelay : (int)(task.delay + task.fixDelta);
                        if (delay > 0)
                        {
                            await Task.Delay(delay, task.ct);
                        }

                        if (isFirstTime)
                        {
                            task.startTime = task.startTime.AddMilliseconds(firstDelay);
                        }
                        else
                        {
                            ++task.loopIndex;

                            // 计算出实际开销时间
                            TimeSpan ts = DateTime.UtcNow - task.startTime;
                            // 修正实际时间值 = 理论开销时间 - 实际开销时间
                            task.fixDelta = (int)(task.delay * task.loopIndex - ts.TotalMilliseconds);
                        }

                        CallBackTaskCB(task);
                    }
                }
            });
        }

        public void HandleTask()
        {
            while (packQue != null && packQue.Count > 0)
            {
                if (packQue.TryDequeue(out AsyncTaskPack pack))
                {
                    pack.cb.Invoke(pack.tid);
                }
                else
                {
                    ErrorFunc?.Invoke("packQue Dequeue data error.");
                }
            }
        }

        private void CallBackTaskCB(AsyncTask task)
        {
            if (setHandle)
            {
                packQue.Enqueue(new AsyncTaskPack(task.tid, task.taskCB));
            }
            else
            {
                task.taskCB.Invoke(task.tid);
            }

            if (task.count == 0)
            {
                if (taskDic.TryRemove(task.tid, out AsyncTask temp))
                {
                    LogFunc?.Invoke($"Task tid:{temp.tid} run to completion.");
                }
                else
                {
                    ErrorFunc?.Invoke($"Remove tid:{task.tid} task in taskDic failed.");
                }

            }
        }

        class AsyncTaskPack
        {
            public int tid;
            public Action<int> cb;

            public AsyncTaskPack(int tid, Action<int> cb)
            {
                this.tid = tid;
                this.cb = cb;
            }
        }

        class AsyncTask
        {
            public int tid;
            public uint delay;
            public uint firstDelay;
            /// <summary>
            /// 执行次数,初期值为0就是一直执行
            /// </summary>
            public int count;
 
            public Action<int> taskCB;
            public Action<int> cancelCB;
            public DateTime startTime;
            /// <summary>
            /// 避免浮点数destTime累加出错
            /// </summary>
            public ulong loopIndex;

            public int fixDelta;

            public CancellationTokenSource cts;
            public CancellationToken ct;

            public AsyncTask(
                int tid,
                uint firstDelay,
                uint delay,
                int count,
                Action<int> taskCB,
                Action<int> cancelCB)
            {
                this.tid = tid;
                this.firstDelay = firstDelay;
                this.delay = delay;
                this.count = count;
                this.taskCB = taskCB;
                this.cancelCB = cancelCB;

                this.startTime = DateTime.UtcNow;
                this.loopIndex = 0;
                this.fixDelta = 0;
                this.cts = new CancellationTokenSource();
                this.ct = cts.Token;
            }
        }
    }
}
