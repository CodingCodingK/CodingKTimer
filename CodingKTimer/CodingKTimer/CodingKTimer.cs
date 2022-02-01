using System;

namespace CodingKTimer
{
    public abstract class CodingKTimer
    {
        protected int m_tid = 0;
        protected abstract int GenerateTid();

        public Action<string> LogFunc;
        public Action<string> WarnFunc;
        public Action<string> ErrorFunc;


        /// <summary>
        /// 创建定时任务
        /// </summary>
        /// <param name="firstDelay">第一次执行任务延迟时长</param>
        /// <param name="taskCB">定时任务回调</param>
        /// <param name="cancelCB">取消任务回调</param>
        /// <param name="count">任务重复次数</param>
        /// /// <param name="delay">定时任务执行频率</param>
        /// <returns>当前计时器唯一任务ID(取消用)</returns>
        public abstract int AddTask(uint firstDelay, Action<int> taskCB, Action<int> cancelCB, uint delay = 0, int count = 1);

        /// <summary>
        /// 删除定时任务
        /// </summary>
        /// <param name="tid">定时任务ID</param>
        /// <returns>删除操作结果</returns>
        public abstract bool DeleteTask(int tid);

        /// <summary>
        /// 重置整个定时器
        /// </summary>
        /// <returns></returns>
        public abstract void Reset();
    }
}
