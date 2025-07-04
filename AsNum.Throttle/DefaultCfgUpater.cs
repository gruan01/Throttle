﻿using System;
using System.Threading.Tasks;

namespace AsNum.Throttle;

/// <summary>
/// 
/// </summary>
public sealed class DefaultCfgUpater : BaseCfgUpdater
{

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    protected override Task Save(TimeSpan newPeriod, int newFrequency)
    {
        return Task.CompletedTask;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="period"></param>
    /// <param name="frequency"></param>
    /// <returns></returns>
    protected override (TimeSpan exPeriod, int exFrquency) Initialize(TimeSpan period, int frequency)
    {
        return (period, frequency);
    }
}
