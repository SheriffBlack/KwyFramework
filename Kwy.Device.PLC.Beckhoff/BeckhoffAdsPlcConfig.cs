using Kwy.Device.PLC.Abstractions;

namespace Kwy.Device.PLC.Beckhoff;

/// <summary>
/// TwinCAT ADS PLC 连接配置。
/// <para>业务点位仍使用 <see cref="PlcPointDefinition"/>；其 <c>Address</c> 为 TwinCAT 符号名，例如 <c>MAIN.Transport.bFixturePresent</c>。</para>
/// </summary>
public sealed class BeckhoffAdsPlcConfig : PlcDeviceRuntimeConfig
{
    /// <summary>目标 TwinCAT 运行时的 AMS Net ID，例如 <c>192.168.1.10.1.1</c>。</summary>
    public string AmsNetId { get; set; } = "127.0.0.1.1.1";

    /// <summary>目标 ADS 服务端口；TwinCAT 3 第一个 PLC Runtime 通常为 851。</summary>
    public int AmsPort { get; set; } = 851;

    /// <summary>单次 ADS 操作超时，单位毫秒。</summary>
    public int OperationTimeoutMilliseconds { get; set; } = 3_000;

    /// <summary>
    /// ADS 经 AMS 路由建立连接；校验仅覆盖 ADS 参数与通用 PLC 运行策略。
    /// </summary>
    public override bool Validate()
    {
        return base.Validate()
            && IsValidAmsNetId(AmsNetId)
            && AmsPort is > 0 and <= ushort.MaxValue
            && OperationTimeoutMilliseconds > 0;
    }

    private static bool IsValidAmsNetId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] segments = value.Split('.', StringSplitOptions.None);
        return segments.Length == 6
            && segments.All(static segment => byte.TryParse(segment, out _));
    }
}
