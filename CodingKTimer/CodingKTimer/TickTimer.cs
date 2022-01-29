using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CodingKTimer
{
    /// <summary>
    /// 毫秒级定时器
    /// </summary>
    public class TickTimer : CodingKTimer
    {
        private const string tidLock = "TickTimer_tidLock";

        private readonly DateTime startDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        private readonly ConcurrentDictionary<int, TickTask> taskDic;
        private readonly Thread timerThread;
        private readonly ConcurrentQueue<TickTaskPack> packQue;
        private readonly bool setHandle;

        /// <summary>
        /// 构造器
        /// </summary>
        /// <param name="interval">循环周期。如果为0或不传，就认为是外部做循环；如果不为0，就内部启用线程执行循环这个TickTimer</param>
        /// <param name="setHandle">是否使用Handle,默认为true</param>
        public TickTimer(int interval = 0, bool setHandle = true)
        {
            taskDic = new ConcurrentDictionary<int, TickTask>();
            this.setHandle = setHandle;

            if (setHandle)
            {
                packQue = new ConcurrentQueue<TickTaskPack>();
            }

            if (interval > 0)
            {
                void StartTick()
                {
                    try
                    {
                        while (true)
                        {
                            UpdateTask();

                            Thread.Sleep(interval);
                        }
                    }
                    catch (ThreadAbortException e)
                    {
                        WarnFunc?.Invoke($"Tick Thread Abort:{e}");
                        throw;
                    }
                }

                // 使用新线程：
                timerThread = new Thread(new ThreadStart(StartTick));
                timerThread.Start();
            }
        }

        public void HandleTask()
        {
            while (packQue != null && packQue.Count > 0)
            {
                if (packQue.TryDequeue(out TickTaskPack pack))
                {
                    pack.cb.Invoke(pack.tid);
                }
                else
                {
                    ErrorFunc?.Invoke("packQue Dequeue data error.");
                }
            }
        }


        /// <summary>
        /// 可以在Unity的Mono中调用这个tick，那么就可以确定性的保证任务是Unity主线程调用的了。
        /// </summary>
        public void UpdateTask()
        {
            double nowTime = GetUTCMs();
            foreach (var item in taskDic)
            {
                TickTask task = item.Value;
                if (nowTime < task.destTime)
                {
                    continue;
                }

                ++task.loopIndex;

                if (task.count > 0)
                {
                    --task.count;
                    if (task.count == 0)
                    {
                        // 线程安全字典，遍历过程中删除无影响。
                        FinishTask(task.tid);
                    }
                    else
                    {
                        // task.destTime += task.delay; 避免浮点数累加误差，所以采用以下方式。
                        task.destTime = task.startTime + task.delay * (task.loopIndex + 1);
                        CallTaskCB(task.tid, task.taskCB);
                    }
                }
                else
                {
                    task.destTime = task.startTime + task.delay * (task.loopIndex + 1);
                    CallTaskCB(task.tid, task.taskCB);
                }
            }
        }

        void FinishTask(int tid)
        {
            if (taskDic.TryRemove(tid, out TickTask task))
            {
                CallTaskCB(tid,task.taskCB);
                task.taskCB = null;
            }
            else
            {
                WarnFunc?.Invoke($"KEY:{tid} remove failed when finished task.");
            }
        }

        void CallTaskCB(int tid, Action<int> taskCB)
        {
            if (setHandle)
            {
                packQue.Enqueue(new TickTaskPack(tid,taskCB));
            }
            else
            {
                taskCB.Invoke(tid);
            }
        }


        private double GetUTCMs()
        {
            TimeSpan ts = DateTime.UtcNow - startDateTime;
            return ts.TotalMilliseconds;
        }

        protected override int GenerateTid()
        {
            lock (tidLock)
            {
                while (true)
                {
                    ++ m_tid;
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


        #region API

        /// <summary>
        /// 创建任务
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="taskCB"></param>
        /// <param name="cancelCB"></param>
        /// <param name="count">如果为0就无限循环，>=1就是次数</param>
        /// <returns></returns>
        public override int AddTask(uint delay, Action<int> taskCB, Action<int> cancelCB, int count = 1)
        {
            int tid = GenerateTid();
            double startTime = GetUTCMs();
            double destTime = startTime + delay;
            TickTask task = new TickTask(tid, delay, count, destTime, taskCB, cancelCB, startTime);

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
            if (taskDic.TryRemove(tid, out TickTask task))
            {
                if (setHandle && task.cancelCB != null)
                {
                    packQue.Enqueue(new TickTaskPack(tid,task.cancelCB));
                }
                else
                {
                    task.cancelCB?.Invoke(tid);
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
            if (!packQue.IsEmpty)
            {
                WarnFunc?.Invoke("Reset:packQue is not empty.");
            }

            taskDic.Clear();
            if (timerThread != null)
            {
                timerThread.Abort();
            }
        }

        #endregion

        class TickTaskPack
        {
            public int tid;
            public Action<int> cb;

            public TickTaskPack(int tid, Action<int> cb)
            {
                this.tid = tid;
                this.cb = cb;
            }
        }


        class TickTask
        {
            public int tid;
            public uint delay;
            /// <summary>
            /// 执行次数,初期值为0就是一直执行
            /// </summary>
            public int count;
            /// <summary>
            /// 下一次的执行时间: StartTime + Delay
            /// </summary>
            public double destTime;
            public Action<int> taskCB;
            public Action<int> cancelCB;
            public double startTime;
            /// <summary>
            /// 避免浮点数destTime累加出错
            /// </summary>
            public ulong loopIndex;

            public TickTask(
                int tid, 
                uint delay, 
                int count, 
                double destTime, // 实际开始时间 = startTime + delay
                Action<int> taskCB, 
                Action<int> cancelCB, 
                double startTime)
            {
                this.tid = tid;
                this.delay = delay;
                this.count = count;
                this.destTime = destTime;
                this.taskCB = taskCB;
                this.cancelCB = cancelCB;
                this.startTime = startTime;
                this.loopIndex = 0;
            }
        }
    }
}
