using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodingKTimer;
using PEUtils;
using UnityEngine;

public class TickTimer_Example : MonoBehaviour
{
    public bool outUpdate = false;
    public bool useHandle = false;
    
    private TickTimer timer;
    /// <summary>
    /// 计时器内部线程驱动内部Update频率
    /// </summary>
    public int timerInternal = 10;
    public uint interval = 66;
    public uint firstDelay = 66;
    public int count = 100;
    int sum = 0;
    int taskId = 0;
    DateTime historyTime;


    // Start is called before the first frame update
    void Start()
    {
        // 如果选择计时器内部线程自己驱动,就10ms一次
        timerInternal = outUpdate ? 0 : timerInternal;
        
        var cfg = new LogConfig() { loggerEnum = LoggerType.Unity};
        PELog.InitSettings(cfg);
        
        timer = new TickTimer(timerInternal, useHandle)
        {
            LogFunc = PELog.Log,
            WarnFunc = PELog.Warn,
            ErrorFunc = PELog.Error,
        };
    }

    // Update is called once per frame
    void Update()
    {
        if (outUpdate)
        {
            timer.UpdateTask();
        }

        if (useHandle)
        {
            timer.HandleTask();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            historyTime = DateTime.UtcNow.AddMilliseconds(firstDelay - interval);
            taskId = timer.AddTask(firstDelay, taskCB, cancelCB, interval, count);
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            PELog.ColorLog(LogColor.Red,"平均间隔：" + sum * 1.0f / count + " ms");
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            timer.DeleteTask(taskId);
        }
    }
    
    void taskCB(int tid)
    {
        // 统计回调执行偏差时间
        DateTime nowTime = DateTime.UtcNow;
        TimeSpan ts = nowTime - historyTime;
        historyTime = nowTime;
        int delta = (int)(ts.TotalMilliseconds - interval);
        PELog.ColorLog(LogColor.Yellow,$"间隔差:{delta} ms");
        sum += Math.Abs(delta);
        PELog.ColorLog(LogColor.Magenta,"tid:{0} working.",tid);
    }

    void cancelCB(int tid)
    {
        PELog.ColorLog(LogColor.Magenta, "tid:{0} canceled.", tid);
    }
}
