using System.Collections.Concurrent;
using KwyTemplate.Flow.Common;
using KwyTemplate.Flow.DataDeals;

namespace KwyTemplate.Flow.Models;

/// <summary>
/// 机台流程中的一个逻辑测试工位。
/// 该模型只描述工位结构、UI 显示列、IO 握手和数据处理器；
/// 不负责创建 PLC、IO 卡、仪表或相机实例。
/// </summary>
/// <remarks>
/// 设备实例由 <c>KwyTemplate.Device</c> 创建并注册，再通过 <c>IMachineDeviceContext</c> 暴露给 Flow 层。
/// 具体机型（例如 <c>MachineDemoPLC</c>）应通过 <c>Devices.GetRequired&lt;TDevice&gt;(deviceId)</c> 获取所需设备，
/// 然后在创建 <see cref="StationDataDeals" /> 时把设备传给对应的 <see cref="IStationDataDeal" /> 实现。
/// 这样工位模型保持轻量，不会把工位元数据和 ADEX、HIOKI 等具体仪表品牌耦合在一起。
/// </remarks>
public sealed class TestStationModel
{
    private int totalCount;
    private int okCount;

    /// <summary>
    /// 工位编号，在当前机台内唯一。
    /// </summary>
    public int StationId { get; set; }


    /// <summary>
    /// Optional machine PLC point key used for station enable switch. When null, StationId is mapped by the machine.
    /// </summary>
    public int? StationSwitchPointKey { get; set; }
    /// <summary>
    /// 工位名称，用于 UI 显示。
    /// </summary>
    public string StationName { get; set; } = string.Empty;

    /// <summary>
    /// 工位完整显示名称的多语言资源 Key。为空时使用 <see cref="StationName" />。
    /// </summary>
    public string? StationNameKey { get; set; }

    /// <summary>
    /// 工位短名称的多语言资源 Key，例如“工位一”。为空时从 <see cref="StationName" /> 推断。
    /// </summary>
    public string? StationShortNameKey { get; set; }

    /// <summary>
    /// 工位设备/测试项显示名称的多语言资源 Key，例如“A面相机”。为空时按测试项或设备 ID 推断。
    /// </summary>
    public string? StationDeviceNameKey { get; set; }

    /// <summary>
    /// 当前工位关联的仪表设备 ID。
    /// UI 层可根据该集合自动生成工站仪表参数页；
    /// 流程执行仍由 StationDataDeals 持有具体仪表实例。
    /// </summary>
    public List<string> InstrumentDeviceIds { get; set; } = [];

    /// <summary>
    /// 工位是否启用。默认启用；后续可由 MES、配方或机型配置下发。
    /// 禁用后该工位不参与轮询触发和手动执行。
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    /// <summary>
    /// 当前工位最近一次测试值，Key 为测试项名称。
    /// DataDeal 在读取、解析仪表/视觉/PLC 数据后更新这里。
    /// </summary>
    public ConcurrentDictionary<string, double> TestValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 当前工位最近一次 OK/NG 判定，Key 为测试项名称。
    /// </summary>
    public ConcurrentDictionary<string, bool> TestJudges { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 当前工位各测试项的软件判定上下限，Key 为测试项名称，例如 DCR1、DCR2。
    /// </summary>
    public ConcurrentDictionary<string, StationMeasurementLimit> TestLimits { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 测试项显示顺序，同时用于生成结果表格列。
    /// </summary>
    public List<string> OrderedTestNames { get; set; } = [];

    /// <summary>
    /// 是否显示到 HomeView 结果表。关闭后该工位仍可参与 IO/流程，但不生成 DataGrid 测试列。
    /// </summary>
    public bool ShowInResultGrid { get; set; } = true;

    /// <summary>
    /// Generate test item names from instrument configuration, such as HIOKI LCR LoadType.
    /// </summary>
    public bool UseInstrumentConfigTestNames { get; set; }

    /// <summary>
    /// 工位 IO 握手绑定。
    /// 常见流程是：测试完成输入的上升沿触发读数，测试结果输入表示 OK/NG，
    /// 读数完成输出通知 PLC 或外部控制器该工位已处理完成。
    /// </summary>
    public StationIoBinding StationIo { get; set; } = new();

    /// <summary>
    /// 工位数据处理器集合。
    /// 真正的仪表读数建议放在具体 DataDeal 中完成。
    /// 例如 DCR 工位可以创建 <c>new InstrumentMeasurementDataDeal("DCR", dcrMeter)</c>，其中 <c>dcrMeter</c>
    /// 由具体机型从 <c>IMachineDeviceContext</c> 取出。
    /// TestStationModel 自己不关心这个 DCR 仪表到底是 ADEX、HIOKI 还是其他品牌。
    /// </summary>
    public List<IStationDataDeal> StationDataDeals { get; set; } = [];

    /// <summary>
    /// 工位可选操作，例如点检、校正、清零、标准件补偿等。
    /// UI 可以根据该集合自动生成操作按钮。
    /// </summary>
    public List<StationOperationDescriptor> Operations { get; } = [];

    /// <summary>
    /// 多个 DataDeal 是否并行执行。
    /// </summary>
    public bool ParallelDeals { get; set; }

    /// <summary>
    /// 当前工位累计测试总数。
    /// </summary>
    public int TotalCount => totalCount;

    /// <summary>
    /// 当前工位累计 OK 数。
    /// </summary>
    public int OkCount => okCount;

    /// <summary>
    /// 当前工位累计 NG 数。
    /// </summary>
    public int NgCount => totalCount - okCount;

    /// <summary>
    /// 当前工位良率。
    /// </summary>
    public double YieldRate => totalCount == 0 ? 0 : (double)okCount / totalCount;

    /// <summary>
    /// 累加一次测试结果统计。
    /// </summary>
    public void AccumulateResult(bool isPass)
    {
        Interlocked.Increment(ref totalCount);
        if (isPass)
        {
            Interlocked.Increment(ref okCount);
        }
    }

    /// <summary>
    /// 清空当前工位统计数据。
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref totalCount, 0);
        Interlocked.Exchange(ref okCount, 0);
    }

    public void SetTestLimit(string testName, double? lowerLimit, double? upperLimit, string? unit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        TestLimits[testName] = new StationMeasurementLimit(lowerLimit, upperLimit, unit);
    }
}


