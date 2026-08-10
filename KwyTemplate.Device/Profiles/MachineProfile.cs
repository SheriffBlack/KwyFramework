namespace KwyTemplate.Device.Profiles;

/// <summary>Declarative description used by the standard configurable machine.</summary>
public sealed class MachineProfile
{
    public string ProfileKey { get; set; } = "Default";
    public string MachineId { get; set; } = "ConfigurableMachine";
    public string MachineName { get; set; } = "通用测包机";
    public int MachinePollingIntervalMs { get; set; } = 5;
    public int IoSnapshotPollingIntervalMs { get; set; } = 5;
    public List<MachineDeviceProfile> Devices { get; set; } = [];
    public List<MachineStationProfile> Stations { get; set; } = [];
    public List<MachineIoPointProfile> IoPoints { get; set; } = [];
    public List<MachinePlcPointProfile> PlcPoints { get; set; } = [];
}

public sealed class MachineDeviceProfile
{
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ConfigurableDeviceKind Kind { get; set; }
    public byte PrimaryAddress { get; set; }
}

public enum ConfigurableDeviceKind
{
    MainPlc,
    MainIoCard,
    BarcodeScanner,
    AdexDcr,
    HiokiLcr
}

public sealed class MachineStationProfile
{
    public int StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool ShowInResultGrid { get; set; } = true;
    public bool UseInstrumentConfigTestNames { get; set; }
    public List<string> InstrumentDeviceIds { get; set; } = [];
    public List<string> TestNames { get; set; } = [];
    public List<string> Operations { get; set; } = [];
    public MachineStationIoProfile Io { get; set; } = new();
}

public sealed class MachineStationIoProfile
{
    // Semantic keys are preferred. Numeric fields are retained as a migration fallback.
    public string? TestFinishedInputPoint { get; set; }
    public string? ResultOkInputPoint { get; set; }
    public string? ResultNgInputPoint { get; set; }
    public string? ResultReadCompletedOutputPoint { get; set; }
    public string? ResultOkOutputPoint { get; set; }
    public string? ResultNgOutputPoint { get; set; }
    public int TestFinishedInput { get; set; } = -1;
    public int ResultOkInput { get; set; } = -1;
    public int ResultNgInput { get; set; } = -1;
    public int ResultReadCompletedOutput { get; set; } = -1;
    public int ResultOkOutput { get; set; } = -1;
    public int ResultNgOutput { get; set; } = -1;
    public string ResultSource { get; set; } = "Hardware";
}

public sealed class MachineIoPointProfile
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public MachineIoPointDirection Direction { get; set; }
    public int Channel { get; set; } = -1;
}

public enum MachineIoPointDirection
{
    Input,
    Output
}

public sealed class MachinePlcPointProfile
{
    public string Key { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataType { get; set; } = "Boolean";
    public bool IsReadOnly { get; set; }
}
