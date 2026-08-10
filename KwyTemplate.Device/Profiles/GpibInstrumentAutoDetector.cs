using System.Text.RegularExpressions;
using Kwy.Communicate.NI;

namespace KwyTemplate.Device.Profiles;

/// <summary>
/// 轻量 GPIB 仪表识别器。
/// 只负责用 *IDN? 判断当前地址上的 DCR 仪表型号，不参与设备生命周期管理。
/// </summary>
internal static partial class GpibInstrumentAutoDetector
{
    private const string IdentifyCommand = "*IDN?\n";

    public static DcrMeterModel? DetectDcrMeter(GpibConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(500, config.Timeout)));
            using var communication = new GpibCommunication(CloneProbeConfig(config));
            communication.ConnectAsync(cts.Token).GetAwaiter().GetResult();
            string response = communication.QueryAsync(IdentifyCommand, cts.Token).GetAwaiter().GetResult();
            communication.DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
            return MatchDcrMeter(response);
        }
        catch
        {
            return null;
        }
    }

    public static DcrMeterModel? MatchDcrMeter(string? identityText)
    {
        if (string.IsNullOrWhiteSpace(identityText))
        {
            return null;
        }

        string normalized = identityText.ToUpperInvariant();
        if (normalized.Contains("HIOKI", StringComparison.Ordinal) || normalized.Contains("3542", StringComparison.Ordinal))
        {
            return DcrMeterModel.HiokiLcr;
        }

        if (normalized.Contains("ADEX", StringComparison.Ordinal)
            || normalized.Contains("1152", StringComparison.Ordinal)
            || normalized.Contains("AX1152", StringComparison.Ordinal))
        {
            return DcrMeterModel.AdexDcr;
        }

        return null;
    }

    public static GpibConfig CreateConfigFromResourceName(string? resourceName, GpibConfig fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return CloneProbeConfig(fallback);
        }

        Match match = GpibResourceRegex().Match(resourceName.Trim());
        if (!match.Success)
        {
            return CloneProbeConfig(fallback);
        }

        var config = CloneProbeConfig(fallback);
        config.BoardNumber = int.Parse(match.Groups["board"].Value);
        config.PrimaryAddress = int.Parse(match.Groups["primary"].Value);
        return config;
    }

    private static GpibConfig CloneProbeConfig(GpibConfig source)
        => new()
        {
            BoardNumber = source.BoardNumber,
            PrimaryAddress = source.PrimaryAddress,
            SecondaryAddress = source.SecondaryAddress,
            Timeout = source.Timeout,
            AutoReconnect = false,
            MaxReconnectAttempts = 0,
            ReconnectInterval = source.ReconnectInterval,
            KeepAlive = false,
            KeepAliveInterval = source.KeepAliveInterval,
            KeepAliveCommand = source.KeepAliveCommand
        };

    [GeneratedRegex(@"^GPIB(?<board>\d+)::(?<primary>\d+)(?:::(?<secondary>\d+))?::INSTR$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GpibResourceRegex();
}
