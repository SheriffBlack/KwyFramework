namespace KwyPecvd.Device.Mfc.Ramp;

/// <summary>定义 MFC 目标值如何转换为硬件设定命令。</summary>
public interface IMfcRampProfile
{
    MfcRampStep SetTarget(double target, TimeSpan duration, DateTimeOffset now);

    /// <summary>
    /// 计算本次 RT 周期的设定值及是否需要写入硬件。
    /// 该方法只生成斜坡决策，不执行硬件通信。
    /// </summary>    
    MfcRampStep Evaluate(DateTimeOffset now);

    /// <summary>停止当前斜坡，并生成保持当前位置所需的决策。</summary>
    MfcRampStep Hold(double feedback, DateTimeOffset now);

    /// <summary>通知 Profile：本次硬件设定值已经成功写入。</summary>
    void ConfirmWritten(double commandSetPoint);
}
