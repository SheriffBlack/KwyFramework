namespace KwyPecvd.Process.Chamber.Vacuum;

/// <summary>
/// 压力计角色标记，Flags支持一个压力计承担多项职责
/// </summary>
[Flags]
public enum ChamberPressureGaugeRole
{
    /// <summary>无角色，未分配用途</summary>
    None = 0,

    /// <summary>
    /// 粗抽PumpDown阶段压力监测，一般为皮拉尼规；
    /// 用于腔体从大气压向下抽真空过程，覆盖大气到低真空区间。
    /// </summary>
    PumpDown = 1 << 0,

    /// <summary>
    /// 工艺压力测量与闭环控制，一般为电容薄膜规CM；
    /// 工艺Run阶段读取压力，参与压力闭环调节，直接决定工艺稳定性。
    /// </summary>
    ProcessControl = 1 << 1,

    /// <summary>
    /// 本底真空(Base Pressure)测量；
    /// 腔体无进气、泵抽至极限真空时，监测腔体本底真空水平，用于腔体检漏、腔体状态确认。
    /// </summary>
    BasePressure = 1 << 2,

    /// <summary>
    /// 独立安全联锁监测压力计；
    /// 独立硬件通道，用于安全保护判断，安全规范常要求不和ProcessControl共用同一只压力计。
    /// </summary>
    Safety = 1 << 3
}
