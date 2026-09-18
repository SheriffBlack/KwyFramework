namespace KwyTemplate.Flow.Common;

/// <summary>
/// 上升沿/下降沿检测器。每次输入当前值，返回本次扫描是否触发。
/// </summary>
public sealed class EdgeTrigger
{
    private bool last;

    public bool Last => last;

    /// <summary>
    /// 上升沿
    /// </summary>
    /// <param name="current"></param>
    /// <returns></returns>
    public bool Rising(bool current)
    {
        bool triggered = current && !last;
        last = current;
        return triggered;
    }

    /// <summary>
    /// 下降沿
    /// </summary>
    /// <param name="current"></param>
    /// <returns></returns>
    public bool Falling(bool current)
    {
        bool triggered = !current && last;
        last = current;
        return triggered;
    }

    public void Reset(bool current = false)
    {
        last = current;
    }
}
