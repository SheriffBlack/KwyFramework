using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.NodeDescriptors;
using KwyTemplate.Vision.Services;
using Kwy.Vision.WPF.Images;
using Kwy.MVVM.Core;
using Kwy.UI.WPF.Services.FileDialogs;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;

using Kwy.ComponentModel;

namespace KwyTemplate.Vision.ViewModels.Items;

/// <summary>
/// 节点运行状态
/// </summary>
public enum NodeStatus
{ Idle, Running, Success, Failed, Paused }

/// <summary>
/// 单个节点的 ViewModel，对应 Nodify NodifyEditor 中的一个 ItemContainer。
/// Location 由 NodifyEditor 的 ItemContainerStyle 通过 ItemContainer.Location 双向绑定。
/// </summary>
public class FlowNodeViewModel : BindableBase
{
    private readonly DataTypeColorService colorService;

    public Guid NodeId { get; }
    public string NodeType { get; }

    private string displayName = string.Empty;

    public string DisplayName
    {
        get => displayName;
        set => SetProperty(ref displayName, value);
    }

    private string nodeDescription = string.Empty;

    public string NodeDescription
    {
        get => nodeDescription;
        set => SetProperty(ref nodeDescription, value);
    }

    // ── 画布坐标（ItemContainer.Location 双向绑定）──
    private Point location;

    public Point Location
    {
        get => location;
        set => SetProperty(ref location, value);
    }

    private Size size;

    public Size Size
    {
        get => size;
        set => SetProperty(ref size, value);
    }

    // ── 选中状态（ItemContainer.IsSelected 双向绑定）──
    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    // ── 节点运行状态（控制状态条颜色）──────────────
    private NodeStatus status = NodeStatus.Idle;

    public NodeStatus Status
    {
        get => status;
        set => SetProperty(ref status, value);
    }

    private object? resultValue;

    public object? ResultValue
    {
        get => resultValue;
        set
        {
            if (SetProperty(ref resultValue, value))
            {
                RaisePropertyChanged(nameof(ResultDisplayValue));
                RaisePropertyChanged(nameof(ShowResultValue));
            }
        }
    }

    public string ResultDisplayValue => ResultValue is null ? string.Empty : FlowValueDisplayFormatter.FormatValue(ResultValue);

    public bool ShowResultValue => ResultValue is not null;

    private TimeSpan? lastElapsed;

    public TimeSpan? LastElapsed
    {
        get => lastElapsed;
        set
        {
            if (SetProperty(ref lastElapsed, value))
            {
                RaisePropertyChanged(nameof(LastElapsedText));
                RaisePropertyChanged(nameof(ShowLastElapsed));
            }
        }
    }

    public bool ShowLastElapsed => LastElapsed.HasValue;

    public string LastElapsedText => LastElapsed.HasValue
        ? $"{LastElapsed.Value.TotalMilliseconds:F0} ms"
        : string.Empty;

    private string? runtimeMessage;

    public string? RuntimeMessage
    {
        get => runtimeMessage;
        set
        {
            if (SetProperty(ref runtimeMessage, value))
            {
                RaisePropertyChanged(nameof(ShowRuntimeMessage));
            }
        }
    }

    public bool ShowRuntimeMessage => !string.IsNullOrWhiteSpace(RuntimeMessage);

    // ── 节点启用/禁用状态（LabVIEW 风格）──────────────
    private bool isDisabled;

    public bool IsDisabled
    {
        get => isDisabled;
        set => SetProperty(ref isDisabled, value);
    }

    private string comment = string.Empty;

    public string Comment
    {
        get => comment;
        set => SetProperty(ref comment, value);
    }

    // ── 端口集合（绑定到 nodify:Node.Input / .Output）──
    public BulkObservableCollection<PortViewModel> InputPorts { get; } = new();

    public BulkObservableCollection<PortViewModel> OutputPorts { get; } = new();

    // ── 分方向端口集合 ──
    public BulkObservableCollection<PortViewModel> InputLeft { get; } = new();

    public BulkObservableCollection<PortViewModel> InputTop { get; } = new();
    public BulkObservableCollection<PortViewModel> OutputRight { get; } = new();
    public BulkObservableCollection<PortViewModel> OutputBottom { get; } = new();

    // ── 参数（右侧属性面板用）──────────────────────
    public BulkObservableCollection<NodeParameterViewModel> Parameters { get; } = new();

    // ── 构造 ──────────────────────────────────────────
    public FlowNodeViewModel(
        FlowNode model,
        DataTypeColorService colorService,
        IReadOnlyList<KwyParameterDefinition>? parameterDefinitions = null,
        IFileDialogService? fileDialogService = null)
    {
        this.colorService = colorService;
        NodeId = model.Id;
        NodeType = model.NodeType;
        DisplayName = model.DisplayName;
        Location = new Point(model.X, model.Y);
        IsDisabled = model.IsDisabled;
        Comment = model.Comment;
        NormalizePorts(model);

        foreach (var parameter in MergeParameters(model.Parameters, parameterDefinitions))
        {
            Parameters.Add(new NodeParameterViewModel(parameter.Definition, parameter.Value, fileDialogService));
        }

        for (int i = 0; i < model.InputPorts.Count; i++)
        {
            AddInputPort(model.InputPorts[i], i, model.InputPorts.Count);
        }

        for (int i = 0; i < model.OutputPorts.Count; i++)
        {
            AddOutputPort(model.OutputPorts[i], i, model.OutputPorts.Count);
        }

        if (InputPorts.Count == 0 && OutputPorts.Count == 0)
        {
            var output = new FlowPort
            {
                Name = "输出",
                Direction = PortDirection.Output,
                DataType = PortDataTypes.Any,
                Side = PortSide.Right
            };
            model.OutputPorts.Add(output);
            AddOutputPort(output, 0, 1);
        }
    }

    private static void NormalizePorts(FlowNode model)
    {
        if (model.NodeType is FlowNodeTypes.VisionLocalImage or FlowNodeTypes.VisionLocalVideo)
        {
            model.OutputPorts.RemoveAll(port =>
                string.Equals(port.Name, FlowPortNames.Images, StringComparison.OrdinalIgnoreCase)
                || string.Equals(port.DataType, PortDataTypes.ImageList, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IEnumerable<(KwyParameterDefinition Definition, object? Value)> MergeParameters(
        IDictionary<string, object?> values,
        IReadOnlyList<KwyParameterDefinition>? definitions)
    {
        var definitionMap = definitions?.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KwyParameterDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitionMap.Values)
        {
            values.TryGetValue(definition.Key, out var value);
            yield return (definition, value ?? definition.DefaultValue);
        }

        foreach (var item in values)
        {
            if (definitionMap.ContainsKey(item.Key))
            {
                continue;
            }

            yield return (KwyParameterDefinition.Create<object?>(item.Key, defaultValue: item.Value), item.Value);
        }
    }

    private void AddInputPort(FlowPort port, int index, int count)
    {
        port.Direction = PortDirection.Input;
        InputPorts.Add(new PortViewModel(port, colorService) { Node = this });

        if (count <= 1)
        {
            InputLeft.Add(new PortViewModel(port, colorService, PortSide.Left) { Node = this });
            InputTop.Add(new PortViewModel(port, colorService, PortSide.Top) { Node = this });
            return;
        }

        if (index == 0)
        {
            InputLeft.Add(new PortViewModel(port, colorService, PortSide.Left) { Node = this });
        }
        else if (index == 1)
        {
            InputTop.Add(new PortViewModel(port, colorService, PortSide.Top) { Node = this });
        }
    }

    private void AddOutputPort(FlowPort port, int index, int count)
    {
        port.Direction = PortDirection.Output;
        OutputPorts.Add(new PortViewModel(port, colorService, PortSide.Right) { Node = this });

        if (count <= 1)
        {
            OutputRight.Add(new PortViewModel(port, colorService, PortSide.Right) { Node = this });
            OutputBottom.Add(new PortViewModel(port, colorService, PortSide.Bottom) { Node = this });
            return;
        }

        if (index == 0)
        {
            OutputRight.Add(new PortViewModel(port, colorService, PortSide.Right) { Node = this });
        }
        else if (index == 1)
        {
            OutputBottom.Add(new PortViewModel(port, colorService, PortSide.Bottom) { Node = this });
        }
    }

    public IEnumerable<PortViewModel> GetVisiblePorts()
        => InputLeft
            .Concat(InputTop)
            .Concat(OutputRight)
            .Concat(OutputBottom);

    public bool TryGetParameterValue(string key, out object? value)
    {
        var parameter = Parameters.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (parameter == null)
        {
            value = null;
            return false;
        }

        value = parameter.Value;
        return true;
    }

    public bool TrySetParameterValue(string key, object? value)
    {
        var parameter = Parameters.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (parameter == null)
        {
            return false;
        }

        parameter.Value = value;
        return true;
    }

    /// <summary>将 VM 状态同步回数据模型（保存前调用）</summary>
    public void SyncToModel(FlowNode model)
    {
        model.DisplayName = DisplayName;
        model.X = Location.X;
        model.Y = Location.Y;
        model.IsDisabled = IsDisabled;
        model.Comment = Comment;

        model.Parameters.Clear();
        foreach (var p in Parameters)
        {
            model.Parameters[p.Key] = p.Value;
        }
    }
}

public class NodeParameterViewModel : BindableBase
{
    private readonly IFileDialogService? fileDialogService;
    private DelegateCommand? browseFileCommand;
    private DelegateCommand? browseFolderCommand;

    public KwyParameterDefinition Definition { get; }

    private string key = string.Empty;

    public string Key
    {
        get => key;
        set => SetProperty(ref key, value);
    }

    private object? parameterValue; // 重命名，避免与 setter 的 value 关键字冲突

    public object? Value
    {
        get => parameterValue;
        set
        {
            object? coerced = CoerceParameterValue(value);
            if (SetProperty(ref parameterValue, coerced))
            {
                RaisePropertyChanged(nameof(HasRequiredError));
                RaisePropertyChanged(nameof(RequiredMark));
                RaisePropertyChanged(nameof(RequiredErrorText));
            }
        }
    }

    public string DisplayName => Definition.DisplayName;

    public string Category => Definition.Category;

    public string? Description => Definition.Description;

    public InputType InputType => Definition.InputType;

    public object? ItemsSource => Definition.ItemsSource;

    public bool IsRequired => Definition.IsRequired;

    public bool IsReadOnly => Definition.IsReadOnly;

    public bool IsInteger => IsIntegerType(Definition.ValueType);

    public double? Minimum => Definition.Minimum;

    public double? Maximum => Definition.Maximum;

    public double SmallChange => Definition.SmallChange ?? (IsInteger ? 1.0 : 0.1);

    public int DecimalPlaces => Definition.DecimalPlaces ?? (IsInteger ? 0 : 3);

    public bool HasRequiredError => IsRequired && IsMissingRequiredValue(Value);

    public string RequiredMark => IsRequired ? "*" : string.Empty;

    public string RequiredErrorText => HasRequiredError ? "必填" : string.Empty;

    public bool IsFilePathParameter
        => string.Equals(Key, FlowParameterKeys.ImagePath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Key, FlowParameterKeys.VideoPath, StringComparison.OrdinalIgnoreCase);

    public DelegateCommand BrowseFileCommand
        => browseFileCommand ??= new DelegateCommand(BrowseFile, () => fileDialogService != null && IsFilePathParameter);

    public DelegateCommand BrowseFolderCommand
        => browseFolderCommand ??= new DelegateCommand(BrowseFolder, () => fileDialogService != null && IsFilePathParameter);

    public NodeParameterViewModel(KwyParameterDefinition definition, object? value, IFileDialogService? fileDialogService = null)
    {
        this.fileDialogService = fileDialogService;
        Definition = definition;
        Key = definition.Key;
        Value = value;
    }

    private void BrowseFile()
    {
        if (fileDialogService == null)
        {
            return;
        }

        string? currentPath = Value?.ToString();
        bool isVideo = string.Equals(Key, FlowParameterKeys.VideoPath, StringComparison.OrdinalIgnoreCase);
        VisionMediaKind mediaKind = GetMediaKind(isVideo);
        string? initialDirectory = VisionMediaFileTypes.ResolveInitialDirectory(currentPath, mediaKind);

        IReadOnlyList<string> selected = fileDialogService.OpenFiles(new OpenFileDialogOptions
        {
            Title = isVideo ? "\u9009\u62e9\u89c6\u9891\u6587\u4ef6" : "\u9009\u62e9\u56fe\u50cf\u6587\u4ef6",
            Filter = VisionMediaFileTypes.CreateOpenFileFilter(mediaKind),
            InitialDirectory = initialDirectory,
            FileName = VisionMediaFileTypes.ResolveInitialFileName(currentPath),
            CheckFileExists = true,
            Multiselect = true
        });

        if (selected.Count > 0)
        {
            Value = VisionMediaFileTypes.JoinSources(selected);
        }
    }

    private void BrowseFolder()
    {
        if (fileDialogService == null)
        {
            return;
        }

        string? currentPath = Value?.ToString();
        bool isVideo = string.Equals(Key, FlowParameterKeys.VideoPath, StringComparison.OrdinalIgnoreCase);
        string? initialDirectory = VisionMediaFileTypes.ResolveInitialDirectory(currentPath, GetMediaKind(isVideo));

        string? selected = fileDialogService.SelectFolder(new FolderDialogOptions
        {
            Title = isVideo ? "\u9009\u62e9\u89c6\u9891\u6587\u4ef6\u5939" : "\u9009\u62e9\u56fe\u50cf\u6587\u4ef6\u5939",
            InitialDirectory = initialDirectory
        });

        if (!string.IsNullOrWhiteSpace(selected))
        {
            Value = selected;
        }
    }

    private static VisionMediaKind GetMediaKind(bool isVideo)
        => isVideo ? VisionMediaKind.Video : VisionMediaKind.Image;

    private static bool IsIntegerType(Type type)
    {
        Type effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        return effectiveType == typeof(byte)
            || effectiveType == typeof(sbyte)
            || effectiveType == typeof(short)
            || effectiveType == typeof(ushort)
            || effectiveType == typeof(int)
            || effectiveType == typeof(uint)
            || effectiveType == typeof(long)
            || effectiveType == typeof(ulong);
    }

    private object? CoerceParameterValue(object? value)
    {
        Type effectiveType = Nullable.GetUnderlyingType(Definition.ValueType) ?? Definition.ValueType;
        if (!IsNumericType(effectiveType) || !TryReadDouble(value, out double number))
        {
            return value;
        }

        if (Definition.Minimum is double minimum && number < minimum)
        {
            number = minimum;
        }

        if (Definition.Maximum is double maximum && number > maximum)
        {
            number = maximum;
        }

        if (IsIntegerType(effectiveType))
        {
            number = Math.Round(number);
        }

        return ConvertNumericValue(number, effectiveType);
    }

    private static object ConvertNumericValue(double value, Type targetType)
        => targetType == typeof(byte) ? (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue)
            : targetType == typeof(sbyte) ? (sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue)
            : targetType == typeof(short) ? (short)Math.Clamp(value, short.MinValue, short.MaxValue)
            : targetType == typeof(ushort) ? (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue)
            : targetType == typeof(int) ? (int)Math.Clamp(value, int.MinValue, int.MaxValue)
            : targetType == typeof(uint) ? (uint)Math.Clamp(value, uint.MinValue, uint.MaxValue)
            : targetType == typeof(long) ? (long)Math.Clamp(value, long.MinValue, long.MaxValue)
            : targetType == typeof(ulong) ? (ulong)Math.Clamp(value, ulong.MinValue, ulong.MaxValue)
            : targetType == typeof(float) ? (float)value
            : targetType == typeof(decimal) ? (decimal)value
            : value;

    private static bool IsNumericType(Type type)
        => IsIntegerType(type)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);

    private static bool TryReadDouble(object? value, out double number)
    {
        number = 0;
        if (value == null)
        {
            return false;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetDouble(out number);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString();
            }
        }

        return value switch
        {
            null => false,
            byte v => Set(v, out number),
            sbyte v => Set(v, out number),
            short v => Set(v, out number),
            ushort v => Set(v, out number),
            int v => Set(v, out number),
            uint v => Set(v, out number),
            long v => Set(v, out number),
            ulong v => Set(v, out number),
            float v => Set(v, out number),
            double v => Set(v, out number),
            decimal v => Set((double)v, out number),
            string text => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number),
            { } other => double.TryParse(other.ToString(), NumberStyles.Float, CultureInfo.CurrentCulture, out number)
                || double.TryParse(other.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
        };
    }

    private static bool Set(double value, out double number)
    {
        number = value;
        return true;
    }

    private static bool IsMissingRequiredValue(object? value)
    {
        if (value == null)
        {
            return true;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()),
                _ => false
            };
        }

        return false;
    }
}
