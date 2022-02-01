using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace CodingKTimer
{
    /// <summary>
    /// 使用外部帧循环调用驱动，并在帧循环中回调
    /// </summary>
    public class FrameTimer : CodingKTimer
    {
        public ulong CurrentFrame => currentFrame;

        private ulong currentFrame;
        private List<int> completedList;
        private const string tidLock = "FrameTimer_tidLock";
        private readonly Dictionary<int, FrameTask> taskDic;

        /// <summary>
        /// 开始
        /// </summary>
        /// <param name="frameId">起始帧数。比如为0，也会执行第0帧的事物（比如添加好的firstDelay = 0 的Task）。</param>
        public FrameTimer(ulong frameId = 0)
        {
            currentFrame = frameId;
            taskDic = new Dictionary<int,FrameTask>();
            completedList = new List<int>();
        }

        public void UpdateTask()
        {
            completedList.Clear();

            foreach (var item in taskDic)
            {
                FrameTask task = item.Value;
                if (task.destFrame <= currentFrame)
                {
                    task.taskCB.Invoke(task.tid);
                    task.destFrame += task.delay;
                    --task.count;

                    if (task.count == 0)
                    {
                        completedList.Add(task.tid);
                    }
                }
            }

            foreach (var tid in completedList)
            {
                if (taskDic.TryGetValue(tid,out FrameTask task))
                {
                    if(taskDic.Remove(tid))
                    {
                        LogFunc?.Invoke($"Task tid:{task.tid} run to completion.");
                    }
                    else
                    {
                        ErrorFunc?.Invoke($"Remove tid:{task.tid} task in taskDic failed.");
                    }
                }
                else
                {
                    ErrorFunc?.Invoke($"Remove tid:{task.tid} task in taskDic is not found.");
                }
            }

            ++currentFrame;
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

        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="firstDelay">第一次执行前的延迟(单位：帧数)，比如为3，那么3帧后也就是第4帧才会调用，而不是第三帧调用！</param>
        /// <param name="taskCB"></param>
        /// <param name="cancelCB"></param>
        /// <param name="delay">执行频率(单位：帧数)</param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int AddTask(uint firstDelay, Action<int> taskCB, Action<int> cancelCB, uint delay = 0, int count = 1)
        {
            int tid = GenerateTid();
            ulong destFrame = currentFrame + firstDelay;
            FrameTask task = new FrameTask(tid,delay,count,destFrame,taskCB,cancelCB);
            
            if (!taskDic.TryGetValue(tid, out var temp))
            {
                taskDic.Add(tid, task);
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
            if (taskDic.TryGetValue(tid, out FrameTask task))
            {
                if (taskDic.Remove(tid))
                {
                    LogFunc?.Invoke($"Remove tid:{task.tid} task in taskDic success.");
                    task.cancelCB?.Invoke(task.tid);
                    return true;
                }
                else
                {
                    WarnFunc?.Invoke($"KEY:{tid} remove failed when custom delete.");
                    return false;
                }
            }
            else
            {
                WarnFunc?.Invoke($"KEY:{tid} cannot founded when remove by DeleteTask.");
                return false;
            }
        }

        public override void Reset()
        {
            taskDic.Clear();
            completedList.Clear();
            currentFrame = 0;
        }

        class FrameTask
        {
            public int tid;
            public uint delay;
            public int count;
            public ulong destFrame;
            public Action<int> taskCB;
            public Action<int> cancelCB;

            public FrameTask(
                int tid,
                uint delay,
                int count,
                ulong destFrame,
                Action<int> taskCB,
                Action<int> cancelCB)
            {
                this.tid = tid;
                this.delay = delay;
                this.count = count;
                this.destFrame = destFrame;
                this.taskCB = taskCB;
                this.cancelCB = cancelCB;
            }
        }
    }
}
