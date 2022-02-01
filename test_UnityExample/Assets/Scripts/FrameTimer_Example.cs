using System;
using System.Collections;
using System.Collections.Generic;
using CodingKTimer;
using PEUtils;
using UnityEngine;

public class FrameTimer_Example : MonoBehaviour
{
    private FrameTimer timer;
    public int count = 100;
    int taskId = 0;
    
    /// <summary>
    /// 定时回调执行频率(单位：帧数)
    /// </summary>
    public uint delay = 2;
    /// <summary>
    /// 定时回调初次延迟等待(单位：帧数)
    /// </summary>
    public uint firstDelay = 10;
    
    // Start is called before the first frame update
    void Start()
    {
        var cfg = new LogConfig() { loggerEnum = LoggerType.Unity};
        PELog.InitSettings(cfg);
        
        timer = new FrameTimer()
        {
            LogFunc = PELog.Log,
            WarnFunc = PELog.Warn,
            ErrorFunc = PELog.Error,
        };
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            PELog.ColorLog(LogColor.Magenta,$"第 {timer.CurrentFrame} 帧添加任务，延迟 {firstDelay} 帧、频率 {delay} 帧、次数 {count} 回。");
            taskId = timer.AddTask(firstDelay, taskCB, cancelCB, delay, count);
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            timer.DeleteTask(taskId);
        }
        
        timer.UpdateTask();
    }

    void taskCB(int tid)
    {
        // 统计回调执行偏差时间
        PELog.ColorLog(LogColor.Yellow,$"第 {timer.CurrentFrame} 帧执行任务，剩余 {--count} 次。");
    }

    void cancelCB(int tid)
    {
        PELog.ColorLog(LogColor.Magenta, "tid:{0} canceled.", tid);
    }
}
