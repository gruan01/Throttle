using System;
using System.Threading.Tasks;

namespace AsNum.Throttle;

/// <summary>
/// 
/// </summary>
public interface IUpdate
{

    /// <summary>
    /// 
    /// </summary>
    /// <param name="period"></param>
    /// <param name="frequency"></param>
    /// <returns></returns>
    Task Update(TimeSpan period, int frequency);

}
