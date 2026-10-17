namespace KwyPecvd.Device.VacuumPump.PfeifferA100;

/// <summary>Pfeiffer A100 泵的静态通信定义。</summary>
public sealed record PfeifferA100Definition
{
    public required VacuumPumpDefinition Pump { get; init; }

    /// <summary>协议使用的三位十进制地址，例如 000。</summary>
    public string Address { get; init; } = "000";

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Pump);
        Pump.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(Address);

        if (Address.Length != 3 || Address.Any(character => !char.IsAsciiDigit(character)))
            throw new ArgumentException(
                "Pfeiffer A100 address must contain exactly three ASCII digits.",
                nameof(Address));
    }
}
