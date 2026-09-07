using System.ComponentModel;
using Kwy.ComponentModel;
using Kwy.MVVM.Core;
using KwyTemplate.Device.Profiles;

namespace KwyTemplate.App.Models;

public sealed class MachineProfileEditorSession
{
    public MachineProfileEditorSession(
        MachineProfile profile,
        MachineRuntimeOptions runtimeOptions,
        Func<IReadOnlyList<string>> getConfigurableProfileKeys,
        Action<string> selectConfigurableProfile)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        RuntimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
        Basic = new MachineBasicEditorModel(Profile, RuntimeOptions, ResizeStations, getConfigurableProfileKeys, selectConfigurableProfile);
        IoPoints = new MachineIoPointsEditorModel(Profile);
        PlcPoints = new MachinePlcPointsEditorModel(Profile);
        RefreshStations();
    }

    public MachineProfile Profile { get; }
    public MachineRuntimeOptions RuntimeOptions { get; }
    public MachineBasicEditorModel Basic { get; }
    public MachineIoPointsEditorModel IoPoints { get; }
    public MachinePlcPointsEditorModel PlcPoints { get; }
    public List<MachineStationEditorModel> Stations { get; private set; } = [];
    public event EventHandler? StructureChanged;

    public void RefreshStations()
    {
        Stations = Profile.Stations.OrderBy(item => item.StationId).Select(item => new MachineStationEditorModel(Profile, item)).ToList();
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResizeStations(int count)
    {
        count = Math.Clamp(count, 1, 64);
        while (Profile.Stations.Count < count)
        {
            int id = Profile.Stations.Count == 0 ? 1 : Profile.Stations.Max(item => item.StationId) + 1;
            Profile.Stations.Add(new MachineStationProfile
            {
                StationId = id,
                StationName = $"工位{id}",
                Operations = ["Check"]
            });
        }

        if (Profile.Stations.Count > count)
        {
            Profile.Stations = Profile.Stations.OrderBy(item => item.StationId).Take(count).ToList();
        }

        RefreshStations();
    }
}

public sealed class MachineBasicEditorModel : BindableBase
{
    private readonly MachineProfile profile;
    private readonly MachineRuntimeOptions runtimeOptions;
    private readonly Action<int> resizeStations;
    private readonly Func<IReadOnlyList<string>> getConfigurableProfileKeys;
    private readonly Action<string> selectConfigurableProfile;

    public MachineBasicEditorModel(
        MachineProfile profile,
        MachineRuntimeOptions runtimeOptions,
        Action<int> resizeStations,
        Func<IReadOnlyList<string>> getConfigurableProfileKeys,
        Action<string> selectConfigurableProfile)
    {
        this.profile = profile;
        this.runtimeOptions = runtimeOptions;
        this.resizeStations = resizeStations;
        this.getConfigurableProfileKeys = getConfigurableProfileKeys;
        this.selectConfigurableProfile = selectConfigurableProfile;
    }

    [Category("基本设定")]
    [DisplayName("机种标识")]
    [InputType(InputType.TextBox)]
    public string MachineKey
    {
        get => profile.ProfileKey;
        set => profile.ProfileKey = value?.Trim() ?? string.Empty;
    }

    [Category("基本设定")]
    [DisplayName("机种名称")]
    [InputType(InputType.TextBox)]
    public string MachineName
    {
        get => profile.MachineName;
        set => profile.MachineName = value?.Trim() ?? string.Empty;
    }

    [Category("基本设定")]
    [DisplayName("工位数")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, 64, DecimalPlaces = 0)]
    public int StationCount
    {
        get => profile.Stations.Count;
        set => resizeStations(value);
    }

    [Category("运行选择")]
    [DisplayName("运行方式")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("Configurable", "Special")]
    public string RunMode
    {
        get => string.Equals(runtimeOptions.ActiveMachineKey, MachineRuntimeOptions.ConfigurableMachineKey, StringComparison.OrdinalIgnoreCase)
            ? "Configurable"
            : "Special";
        set
        {
            if (string.Equals(value, "Configurable", StringComparison.OrdinalIgnoreCase))
            {
                runtimeOptions.ActiveMachineKey = MachineRuntimeOptions.ConfigurableMachineKey;
                runtimeOptions.ActiveProfileKey = profile.ProfileKey;
            }
            else if (string.Equals(value, "Special", StringComparison.OrdinalIgnoreCase)
                && string.Equals(runtimeOptions.ActiveMachineKey, MachineRuntimeOptions.ConfigurableMachineKey, StringComparison.OrdinalIgnoreCase))
            {
                runtimeOptions.ActiveMachineKey = "Machine_4_HAHH";
            }
        }
    }

    [Category("运行选择")]
    [DisplayName("配置化机型")]
    [InputType(InputType.ComboBox)]
    [ItemsSourceProvider(nameof(ConfigurableProfileKeys))]
    public string ConfigurableProfileKey
    {
        get => profile.ProfileKey;
        set
        {
            if (!string.IsNullOrWhiteSpace(value)
                && !string.Equals(value, profile.ProfileKey, StringComparison.OrdinalIgnoreCase))
            {
                selectConfigurableProfile(value);
            }
        }
    }

    // 仅作为 ConfigurableProfileKey 的下拉数据源，不属于可编辑的机种配置字段。
    [Browsable(false)]
    public IReadOnlyList<string> ConfigurableProfileKeys => getConfigurableProfileKeys();

    [Category("运行选择")]
    [DisplayName("特殊机型")]
    [InputType(InputType.ComboBox)]
    [ItemsSource("Machine_4_HAHH", "Machine_2_A")]
    public string SpecialMachineKey
    {
        get => string.Equals(runtimeOptions.ActiveMachineKey, MachineRuntimeOptions.ConfigurableMachineKey, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : runtimeOptions.ActiveMachineKey;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                runtimeOptions.ActiveMachineKey = value;
            }
        }
    }
}

public sealed class MachineStationEditorModel
{
    private readonly MachineProfile profile;
    private readonly MachineStationProfile station;

    public MachineStationEditorModel(MachineProfile profile, MachineStationProfile station)
    {
        this.profile = profile;
        this.station = station;
    }

    [Category("基本设定")]
    [DisplayName("工位编号")]
    [ReadOnly(true)]
    public int StationId => station.StationId;

    [Category("基本设定")]
    [DisplayName("工位名称")]
    [InputType(InputType.TextBox)]
    public string StationName { get => station.StationName; set => station.StationName = value?.Trim() ?? string.Empty; }

    [Category("基本设定")]
    [DisplayName("启用")]
    [InputType(InputType.ToggleButton)]
    public bool IsEnabled { get => station.IsEnabled; set => station.IsEnabled = value; }

    [Category("仪表")]
    [DisplayName("仪表设备 ID")]
    [InputType(InputType.TextBox)]
    public string InstrumentDeviceId
    {
        get => station.InstrumentDeviceIds.FirstOrDefault() ?? string.Empty;
        set => station.InstrumentDeviceIds = string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];
    }

    [Category("仪表")]
    [DisplayName("测试项目")]
    [InputType(InputType.TextBox)]
    public string TestName
    {
        get => string.Join(",", station.TestNames);
        set => station.TestNames = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    [Category("板卡输入")]
    [DisplayName("测试结束点位")]
    [InputType(InputType.NumberBox)]
    [NumberRange(-1, 63, DecimalPlaces = 0)]
    public int TestFinishedInput { get => GetChannel(nameof(MachineStationIoProfile.TestFinishedInputPoint), station.Io.TestFinishedInput); set => SetInputPoint(nameof(MachineStationIoProfile.TestFinishedInputPoint), value, "TestFinished"); }

    [Category("板卡输入")]
    [DisplayName("OK 点位")]
    [InputType(InputType.NumberBox)]
    [NumberRange(-1, 63, DecimalPlaces = 0)]
    public int ResultOkInput { get => GetChannel(nameof(MachineStationIoProfile.ResultOkInputPoint), station.Io.ResultOkInput); set => SetInputPoint(nameof(MachineStationIoProfile.ResultOkInputPoint), value, "ResultOk"); }

    [Category("板卡输入")]
    [DisplayName("NG 点位")]
    [InputType(InputType.NumberBox)]
    [NumberRange(-1, 63, DecimalPlaces = 0)]
    public int ResultNgInput { get => GetChannel(nameof(MachineStationIoProfile.ResultNgInputPoint), station.Io.ResultNgInput); set => SetInputPoint(nameof(MachineStationIoProfile.ResultNgInputPoint), value, "ResultNg"); }

    [Category("板卡输出")]
    [DisplayName("读取结束点位")]
    [InputType(InputType.NumberBox)]
    [NumberRange(-1, 63, DecimalPlaces = 0)]
    public int ResultReadCompletedOutput { get => GetChannel(nameof(MachineStationIoProfile.ResultReadCompletedOutputPoint), station.Io.ResultReadCompletedOutput); set => SetOutputPoint(nameof(MachineStationIoProfile.ResultReadCompletedOutputPoint), value, "ReadCompleted"); }

    private int GetChannel(string propertyName, int fallback)
    {
        string? key = (string?)typeof(MachineStationIoProfile).GetProperty(propertyName)?.GetValue(station.Io);
        return string.IsNullOrWhiteSpace(key) ? fallback : profile.IoPoints.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))?.Channel ?? fallback;
    }

    private void SetInputPoint(string propertyName, int channel, string suffix)
        => SetPoint(propertyName, channel, suffix, MachineIoPointDirection.Input);

    private void SetOutputPoint(string propertyName, int channel, string suffix)
        => SetPoint(propertyName, channel, suffix, MachineIoPointDirection.Output);

    private void SetPoint(string propertyName, int channel, string suffix, MachineIoPointDirection direction)
    {
        if (channel < 0)
        {
            typeof(MachineStationIoProfile).GetProperty(propertyName)?.SetValue(station.Io, null);
            return;
        }

        string key = $"Station{station.StationId}.{suffix}";
        MachineIoPointProfile point = profile.IoPoints.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? new MachineIoPointProfile { Key = key };
        point.Channel = channel;
        point.Direction = direction;
        point.DisplayName = $"{station.StationName}{suffix}";
        if (!profile.IoPoints.Contains(point))
        {
            profile.IoPoints.Add(point);
        }

        typeof(MachineStationIoProfile).GetProperty(propertyName)?.SetValue(station.Io, key);
    }
}

public sealed class MachineIoPointsEditorModel : BindableBase
{
    private readonly MachineProfile profile;
    private MachineIoPointProfile? selectedPoint;

    public MachineIoPointsEditorModel(MachineProfile profile)
    {
        this.profile = profile;
        Points = new System.Collections.ObjectModel.ObservableCollection<MachineIoPointProfile>(profile.IoPoints);
        AddPointCommand = new DelegateCommand(AddPoint);
        RemovePointCommand = new DelegateCommand(RemovePoint, () => SelectedPoint != null);
    }

    public System.Collections.ObjectModel.ObservableCollection<MachineIoPointProfile> Points { get; }
    public MachineIoPointProfile? SelectedPoint
    {
        get => selectedPoint;
        set
        {
            if (SetProperty(ref selectedPoint, value))
            {
                RemovePointCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DelegateCommand AddPointCommand { get; }
    public DelegateCommand RemovePointCommand { get; }

    private void AddPoint()
    {
        int channel = NextChannel(MachineIoPointDirection.Input);
        Points.Add(new MachineIoPointProfile
        {
            Key = $"IoInput{channel}",
            DisplayName = $"输入点 {channel}",
            Direction = MachineIoPointDirection.Input,
            Channel = channel
        });
        Synchronize();
    }

    private void RemovePoint()
    {
        if (SelectedPoint == null)
        {
            return;
        }

        Points.Remove(SelectedPoint);
        Synchronize();
    }

    private int NextChannel(MachineIoPointDirection direction)
        => Points.Where(point => point.Direction == direction).Select(point => point.Channel).DefaultIfEmpty(-1).Max() + 1;

    private void Synchronize() => profile.IoPoints = Points.ToList();
}

public sealed class MachinePlcPointsEditorModel : BindableBase
{
    private readonly MachineProfile profile;
    private MachinePlcPointProfile? selectedPoint;

    public MachinePlcPointsEditorModel(MachineProfile profile)
    {
        this.profile = profile;
        Points = new System.Collections.ObjectModel.ObservableCollection<MachinePlcPointProfile>(profile.PlcPoints);
        AddPointCommand = new DelegateCommand(AddPoint);
        RemovePointCommand = new DelegateCommand(RemovePoint, () => SelectedPoint != null);
    }

    public System.Collections.ObjectModel.ObservableCollection<MachinePlcPointProfile> Points { get; }
    public MachinePlcPointProfile? SelectedPoint
    {
        get => selectedPoint;
        set
        {
            if (SetProperty(ref selectedPoint, value))
            {
                RemovePointCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DelegateCommand AddPointCommand { get; }
    public DelegateCommand RemovePointCommand { get; }

    private void AddPoint()
    {
        int number = Points.Count + 1;
        Points.Add(new MachinePlcPointProfile
        {
            Key = $"PlcPoint{number}",
            DisplayName = $"PLC 点位 {number}",
            Address = "M0"
        });
        Synchronize();
    }

    private void RemovePoint()
    {
        if (SelectedPoint == null)
        {
            return;
        }

        Points.Remove(SelectedPoint);
        Synchronize();
    }

    private void Synchronize() => profile.PlcPoints = Points.ToList();
}
