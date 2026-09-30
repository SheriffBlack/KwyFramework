using System.Text.RegularExpressions;

namespace Kwy.Communicate.Visa;

/// <summary>通过 VISA 访问 GPIB 仪器的强类型配置。</summary>
public sealed partial class GpibConfig : VisaConfig
{
    /// <summary>初始化板卡号为 0、仪器主地址为 1 的默认配置。</summary>
    public GpibConfig() => ResourceName = "GPIB0::1::INSTR";

    /// <summary>获取或设置 GPIB 接口板卡号。</summary>
    public int BoardNumber
    {
        get => Parse().Board;
        set => ResourceName = Format(value, PrimaryAddress, SecondaryAddress);
    }

    /// <summary>获取或设置仪器主地址，有效范围为 0～30。</summary>
    public int PrimaryAddress
    {
        get => Parse().Primary;
        set => ResourceName = Format(BoardNumber, value, SecondaryAddress);
    }

    /// <summary>获取或设置仪器副地址，有效范围为 0～30；设置为 0 表示不使用副地址。</summary>
    public int SecondaryAddress
    {
        get => Parse().Secondary;
        set => ResourceName = Format(BoardNumber, PrimaryAddress, value);
    }

    /// <inheritdoc />
    public override bool Validate()
    {
        (int board, int primary, int secondary) = Parse();
        return base.Validate() && board is >= 0 and <= 255
            && primary is >= 0 and <= 30 && secondary is >= 0 and <= 30
            && GpibResourcePattern().IsMatch(ResourceName);
    }

    private (int Board, int Primary, int Secondary) Parse()
    {
        Match match = GpibResourcePattern().Match(ResourceName);
        return match.Success
            ? (int.Parse(match.Groups["board"].Value), int.Parse(match.Groups["primary"].Value),
                match.Groups["secondary"].Success ? int.Parse(match.Groups["secondary"].Value) : 0)
            : (-1, -1, -1);
    }

    private static string Format(int board, int primary, int secondary)
    {
        if (board is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(board));
        if (primary is < 0 or > 30) throw new ArgumentOutOfRangeException(nameof(primary));
        if (secondary is < 0 or > 30) throw new ArgumentOutOfRangeException(nameof(secondary));
        return secondary == 0
            ? $"GPIB{board}::{primary}::INSTR"
            : $"GPIB{board}::{primary}::{secondary}::INSTR";
    }

    [GeneratedRegex(@"^GPIB(?<board>\d+)::(?<primary>\d+)(?:::(?<secondary>\d+))?::INSTR$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GpibResourcePattern();
}
