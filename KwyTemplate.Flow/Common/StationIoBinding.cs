namespace KwyTemplate.Flow.Common;

/// <summary>
/// 工位结果来源。
/// </summary>
public enum StationResultSource
{
    /// <summary>
    /// 由外部硬件或仪表比较器通过 IO 输入给出 OK/NG。
    /// </summary>
    Hardware,

    /// <summary>
    /// 由 PC 侧 DataDeal 根据测试值、上下限、配方或补偿规则判定 OK/NG。
    /// </summary>
    Software
}

/// <summary>
/// 工位标准 IO 握手绑定。
/// 默认兼容旧机型：硬件比较结果输入 + PC 完成输出。
/// </summary>
public sealed class StationIoBinding
{
    /// <summary>
    /// 输入信号：外部控制器通知该工位测试完成。
    /// 默认读取该点位的上升沿触发数据读取；小于 0 表示未配置。
    /// </summary>
    public int TestFinishedInput { get; set; } = -1;

    /// <summary>
    /// 工位结果来源。默认使用硬件输入，保持与原有机型一致。
    /// </summary>
    public StationResultSource ResultSource { get; set; } = StationResultSource.Hardware;

    /// <summary>
    /// 硬件结果 OK 输入。Hardware 模式下使用；小于 0 表示未配置。
    /// </summary>
    public int ResultOkInput { get; set; } = -1;

    /// <summary>
    /// 硬件结果 NG 输入。Hardware 模式下使用；小于 0 表示未配置。
    /// </summary>
    public int ResultNgInput { get; set; } = -1;

    /// <summary>
    /// 旧字段兼容：等价于 <see cref="ResultOkInput" />。
    /// </summary>
    public int TestResultInput
    {
        get => ResultOkInput;
        set => ResultOkInput = value;
    }

    /// <summary>
    /// 输出信号：PC 通知外部控制器该工位结果已读取并处理完成。
    /// 小于 0 表示未配置，不输出完成握手。
    /// </summary>
    public int ResultReadCompletedOutput { get; set; } = -1;

    /// <summary>
    /// 输出信号：PC 判定 OK 后写给外部控制器。Software 模式常用；小于 0 表示未配置。
    /// </summary>
    public int ResultOkOutput { get; set; } = -1;

    /// <summary>
    /// 输出信号：PC 判定 NG 后写给外部控制器。Software 模式常用；小于 0 表示未配置。
    /// </summary>
    public int ResultNgOutput { get; set; } = -1;
}
