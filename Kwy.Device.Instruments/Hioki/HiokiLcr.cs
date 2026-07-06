using Kwy.Communicate.Abstractions;
using Kwy.Device.Core.Instrument;
using System.Text;

namespace Kwy.Device.Instruments.Hioki;

/// <summary>
/// HIOKI LCR测试仪通用驱动 (支持 IM3533, IM3570, IM3536 等)
/// 统一使用 HiokiLcrConfig 模型
/// </summary>
public class HiokiLcr : InstrumentBase
{
    public override string DeviceModel => "HIOKI_LCR";

    public HiokiLcr(string deviceId, string deviceName, IProtocolConfig protocolConfig, ICommunicationFactory? factory = null)
        : base(deviceId, deviceName, protocolConfig, factory)
    {
    }

    public HiokiLcr(string deviceId, string deviceName, ICommunicationClient protocol)
        : base(deviceId, deviceName, protocol)
    {
    }

    /// <summary>
    /// 统一拼接指令逻辑
    /// </summary>
    public override string JoinCommand()
    {
        if (DeviceParameter is not HiokiLcrConfig config)
            return string.Empty;

        StringBuilder sb = new StringBuilder();

        // 1. 模式设定
        sb.Append(":MODE LCR;");

        // 2. 配置 4 路参数位 (Z, Ls, Phase 等)
        sb.Append($":PARameter1 {MapParameter(config.Parameter1)};");
        sb.Append($":PARameter2 {MapParameter(config.Parameter2)};");
        sb.Append($":PARameter3 {MapParameter(config.Parameter3)};");
        sb.Append($":PARameter4 {MapParameter(config.Parameter4)};");

        // 3. 配置测试物理条件
        sb.Append($":FREQuency {config.Frequency};");
        sb.Append($":LEVel {config.Voltage};");
        sb.Append($":SPEEd {config.Speed};");
        sb.Append($":RANGe {config.Range};");
        sb.Append($":COMParator {config.Comparator};");

        // 4. 触发设置
        sb.Append($":TRIGger {config.TriggerMode};");
        sb.Append($":TRIGger:DELay {config.Delay};");

        // 5.上下限设置
        sb.Append($":COMPARATOR:FLIMIT:ABSOLUTE {config.Parameter1Max},{config.Parameter1Min};");
        sb.Append($":COMPARATOR:SLIMIT:ABSOLUTE {config.Parameter3Max},{config.Parameter3Min};");

        return sb.ToString();
    }

    protected override string ParseResponse(ReadOnlySpan<byte> responseBytes)
    {
        return base.ParseResponse(responseBytes);
    }

    /// <summary>
    /// 映射字符串参数为 HIOKI 指令格式
    /// </summary>
    private string MapParameter(string param)
    {
        if (string.IsNullOrEmpty(param)) return "OFF";

        return param.ToUpper() switch
        {
            "L_S" => "LS",
            "L_P" => "LP",
            "C_S" => "CS",
            "C_P" => "CP",
            "R_S" => "RS",
            "R_P" => "RP",
            "PHAS" => "PHASE",
            _ => param.ToUpper()
        };
    }
}
