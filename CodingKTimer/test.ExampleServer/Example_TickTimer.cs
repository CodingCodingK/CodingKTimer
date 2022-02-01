using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodingKTimer;
using PEUtils;

namespace test.ExampleServer
{
    internal class Example_TickTimer
    {
        static void Main(string[] args)
        {

            PELog.InitSettings();
            PELog.ColorLog(LogColor.Green,"test Starting...");

            // MinMissTest(66);

            // TickTimerExample(false,false);
            // AsyncTimerExample(false);
            // FrameTimerExample();

            Console.ReadKey();
        }

        static void TickTimerExample(bool useHandle,bool outUpdate)
        {
            int timerInternal = outUpdate ? 0 : 10;
            uint interval = 66;
            int count = 100;
            int sum = 0;
            int taskId = 0;
            uint firstDelay = 1000;

            // 统计回调执行偏差时间
            DateTime historyTime;
            void taskCB(int tid)
            {
                DateTime nowTime = DateTime.UtcNow;
                TimeSpan ts = nowTime - historyTime;
                historyTime = nowTime;
                int delta = (int)(ts.TotalMilliseconds - interval);
                PELog.ColorLog(LogColor.Yellow,$"间隔差:{delta} ms");
                sum += Math.Abs(delta);
                PELog.ColorLog(LogColor.Magenta,"tid:{0} working.",tid);
            };

            void cancelCB(int tid)
            {
                PELog.ColorLog(LogColor.Magenta, "tid:{0} canceled.", tid);
            };

            TickTimer timer = new TickTimer(timerInternal, useHandle)
            {
                LogFunc = PELog.Log,
                WarnFunc = PELog.Warn,
                ErrorFunc = PELog.Error,
            };

            Task.Run(async () =>
            {
                await Task.Delay(3000);
                historyTime = DateTime.UtcNow.AddMilliseconds(firstDelay - interval);

                taskId = timer.AddTask(firstDelay, taskCB, cancelCB, interval, count);
            });

            if (useHandle || outUpdate)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    PELog.Log("Handle Start.");
                    while (true)
                    {
                        if (outUpdate)
                        {
                            timer.UpdateTask();
                        }

                        if (useHandle)
                        {
                            timer.HandleTask();
                        }
                        
                        await Task.Delay(2);
                    }
                });
            }



            while (true)
            {
                if (Console.ReadLine() == "calc")
                {
                    PELog.ColorLog(LogColor.Red,"平均间隔：" + sum * 1.0f / count + " ms");
                }
                else if (Console.ReadLine() == "del")
                {
                    timer.DeleteTask(taskId);
                }
            }
        }


        static void AsyncTimerExample(bool useHandle)
        {
            uint interval = 66;
            int count = 100;
            int sum = 0;
            int taskId = 0;
            var completed = false;
            uint firstDelay = 1000;

            // 统计回调执行偏差时间
            DateTime historyTime;
            void taskCB(int tid)
            {
                DateTime nowTime = DateTime.UtcNow;
                TimeSpan ts = nowTime - historyTime;
                historyTime = nowTime;
                int delta = (int)(ts.TotalMilliseconds - interval);
                PELog.ColorLog(LogColor.Yellow, $"间隔差:{delta} ms");
                sum += Math.Abs(delta);
                PELog.ColorLog(LogColor.Magenta, "tid:{0} working.", tid);
            };

            void cancelCB(int tid)
            {
                PELog.ColorLog(LogColor.Magenta, "tid:{0} canceled.", tid);
                completed = true;
            };

            AsyncTimer timer = new AsyncTimer(useHandle)
            {
                LogFunc = PELog.Log,
                WarnFunc = PELog.Warn,
                ErrorFunc = PELog.Error,
            };

            Task.Run(async () =>
            {
                await Task.Delay(2000);
                historyTime = DateTime.UtcNow.AddMilliseconds(firstDelay - interval);

                taskId = timer.AddTask(firstDelay, taskCB, cancelCB, interval, count);
            });

            if (useHandle)
            {
                PELog.Log("Handle Start.");
                while (true)
                {
                    Thread.Sleep(5);
                    timer.HandleTask();
                    if (completed)
                    {
                        break;
                    }
                }
            }

            while (true)
            {

                PELog.Log("Input Start.");
                if (Console.ReadLine() == "calc")
                {
                    PELog.ColorLog(LogColor.Red, "平均间隔：" + sum * 1.0f / count + " ms");
                }
                else if (Console.ReadLine() == "del")
                {
                    timer.DeleteTask(taskId);
                }
            }
        }

        static void FrameTimerExample(int serverFrameInterval = 66)
        {
            int interval = serverFrameInterval;
            int taskId = 0;
            int count = 100;
            int sum = 0;
            var completed = false;
            uint firstDelay = 10;
            uint delay = 2;

            // 统计回调执行偏差时间
            DateTime historyTime;
            void taskCB(int tid)
            {
                DateTime nowTime = DateTime.UtcNow;
                TimeSpan ts = nowTime - historyTime;
                historyTime = nowTime;
                int delta = (int)(ts.TotalMilliseconds - interval * delay);
                PELog.ColorLog(LogColor.Yellow, $"间隔差:{delta} ms");
                sum += Math.Abs(delta);
                //PELog.ColorLog(LogColor.Magenta, "tid:{0} working.", tid);
            };

            void cancelCB(int tid)
            {
                PELog.ColorLog(LogColor.Magenta, "tid:{0} canceled.", tid);
                completed = true;
            };

            FrameTimer timer = new FrameTimer()
            {
                LogFunc = PELog.Log,
                WarnFunc = PELog.Warn,
                ErrorFunc = PELog.Error,
            };

            Task.Run(async () =>
            {
                await Task.Delay(2000);
                // 第一次的误差 = (10/2)倍后续循环误差
                historyTime = DateTime.UtcNow.AddMilliseconds(firstDelay * interval).AddMilliseconds(-delay * interval);

                taskId = timer.AddTask(firstDelay, taskCB, cancelCB, delay, count);

                while (true)
                {
                    timer.UpdateTask();
                    Thread.Sleep(interval);
                    if (completed)
                    {
                        break;
                    }
                }
            });

            while (true)
            {
                if (Console.ReadLine() == "calc")
                {
                    PELog.ColorLog(LogColor.Red, "平均间隔：" + sum * 1.0f / count + " ms");
                }
                else if (Console.ReadLine() == "del")
                {
                    timer.DeleteTask(taskId);
                }
            }

        }

        /// <summary>
        /// 不使用定时器也会产生的Thread.Sleep误差 测试
        /// </summary>
        /// <param name="interval"></param>
        static void MinMissTest(int interval)
        {
            int count = 50;
            DateTime historyTime;
            Thread.Sleep(2000);

            historyTime = DateTime.UtcNow.AddMilliseconds(-interval);
            while (true)
            {
                DateTime nowTime = DateTime.UtcNow;
                TimeSpan ts = nowTime - historyTime;
                historyTime = nowTime;
                int delta = (int)(ts.TotalMilliseconds - interval);
                Console.WriteLine($"间隔差:{delta} ms");

                Thread.Sleep(interval);
            }
        }
    }
}
