using Kwy.ComponentModel;
using Kwy.Vision.Abstractions.Algorithms;
using KwyTemplate.Vision.Models;

namespace KwyTemplate.Vision.NodeDescriptors;

public sealed class LocalImageInputDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.VisionLocalImage;
    public string DisplayName => "本地图像";
    public string Category => "视觉输入";
    public string Description => "从本地图片文件读取一张图像，适合离线调试和单张样本验证。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<string>(
            FlowParameterKeys.ImagePath,
            displayName: "图像路径",
            defaultValue: string.Empty,
            description: "支持 WPF BitmapDecoder 可读取的常见图片格式。",
            isRequired: true)
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        OutputPorts = [new FlowPort { Name = FlowPortNames.Image, Direction = PortDirection.Output, DataType = PortDataTypes.Image }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class LocalVideoInputDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.VisionLocalVideo;
    public string DisplayName => "本地视频";
    public string Category => "视觉输入";
    public string Description => "从本地视频文件抽帧输出图像，适合离线复现连续采集场景。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<string>(
            FlowParameterKeys.VideoPath,
            displayName: "视频路径",
            defaultValue: string.Empty,
            description: "预留给视频解码器接入，后续可按帧号或时间取图。",
            isRequired: true),
        KwyParameterDefinition.Create<int>(
            FlowParameterKeys.FrameIndex,
            displayName: "帧号",
            defaultValue: 0,
            description: "从 0 开始的抽帧序号。",
            minimum: 0,
            smallChange: 1,
            decimalPlaces: 0)
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        OutputPorts = [new FlowPort { Name = FlowPortNames.Image, Direction = PortDirection.Output, DataType = PortDataTypes.Image }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class CameraCaptureInputDescriptor : IFlowNodeDescriptor
{
    private static readonly string[] TriggerModes = ["Continuous", "SoftwareTrigger", "HardwareTrigger"];

    public string NodeType => FlowNodeTypes.VisionCameraCapture;
    public string DisplayName => "相机采集";
    public string Category => "视觉输入";
    public string Description => "从本机摄像头、USB 相机或设备管理中的相机实例采集图像。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<string>(
            FlowParameterKeys.CameraName,
            displayName: "相机名称/索引",
            defaultValue: string.Empty,
            description: "可填写设备管理中的相机名称，也可填写本机摄像头索引，如 0。",
            isRequired: true),
        KwyParameterDefinition.Create<string>(
            FlowParameterKeys.TriggerMode,
            displayName: "触发模式",
            defaultValue: "Continuous",
            inputType: InputType.ComboBox,
            itemsSource: TriggerModes),
        KwyParameterDefinition.Create<double>(
            FlowParameterKeys.ExposureMs,
            displayName: "曝光(ms)",
            defaultValue: 10.0,
            minimum: 0.0,
            smallChange: 0.1,
            decimalPlaces: 3),
        KwyParameterDefinition.Create<double>(
            FlowParameterKeys.Gain,
            displayName: "增益",
            defaultValue: 1.0,
            minimum: 0.0,
            smallChange: 0.1,
            decimalPlaces: 3)
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        OutputPorts = [new FlowPort { Name = FlowPortNames.Image, Direction = PortDirection.Output, DataType = PortDataTypes.Image }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class ImagePreprocessDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.VisionImagePreprocess;
    public string DisplayName => "图像预处理";
    public string Category => "图像处理";
    public string Description => "均值、Median、Gaussian、形态学、灰度增强等预处理。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<string>(
            FlowParameterKeys.Operation,
            displayName: "方法",
            defaultValue: VisionPreprocessOperation.Mean.ToString(),
            inputType: InputType.ComboBox,
            itemsSource: Enum.GetNames<VisionPreprocessOperation>()),
        KwyParameterDefinition.Create<double>(
            FlowParameterKeys.Radius,
            displayName: "半径/模板尺寸",
            defaultValue: 3.0,
            description: "会自动取不小于 1 的整数模板尺寸。",
            minimum: 1.0,
            smallChange: 1.0,
            decimalPlaces: 0),
        .. VisionRoiParameterDefinitions.Create()
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowPortNames.ImageInput, Direction = PortDirection.Input, DataType = PortDataTypes.Image }],
        OutputPorts = [new FlowPort { Name = FlowPortNames.ImageOutput, Direction = PortDirection.Output, DataType = PortDataTypes.Image }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class ThresholdSegmentationDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.VisionThreshold;
    public string DisplayName => "阈值分割";
    public string Category => "图像处理";
    public string Description => "按灰度上下限分割区域。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<double>(FlowParameterKeys.ThresholdLower, displayName: "下限", defaultValue: 128.0, minimum: 0.0, maximum: 255.0, smallChange: 1.0, decimalPlaces: 0),
        KwyParameterDefinition.Create<double>(FlowParameterKeys.ThresholdUpper, displayName: "上限", defaultValue: 255.0, minimum: 0.0, maximum: 255.0, smallChange: 1.0, decimalPlaces: 0),
        .. VisionRoiParameterDefinitions.Create()
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowPortNames.ImageInput, Direction = PortDirection.Input, DataType = PortDataTypes.Image }],
        OutputPorts = [new FlowPort { Name = FlowPortNames.Region, Direction = PortDirection.Output, DataType = PortDataTypes.Region }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class BlobInspectionDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.VisionBlob;
    public string DisplayName => "Blob 分析";
    public string Category => "缺陷检测";
    public string Description => "提取连通区域并输出 Blob 特征集合。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<double>(FlowParameterKeys.MinArea, displayName: "最小面积", defaultValue: 10.0, minimum: 0.0, smallChange: 1.0, decimalPlaces: 0),
        KwyParameterDefinition.Create<double>(FlowParameterKeys.MaxArea, displayName: "最大面积", defaultValue: 999999.0, minimum: 0.0, smallChange: 100.0, decimalPlaces: 0),
        .. VisionRoiParameterDefinitions.Create()
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowPortNames.ImageInput, Direction = PortDirection.Input, DataType = PortDataTypes.Image }],
        OutputPorts = [new FlowPort { Name = FlowPortNames.Blobs, Direction = PortDirection.Output, DataType = PortDataTypes.BlobList }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class CaliperMeasurementDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.VisionCaliper;
    public string DisplayName => "卡尺测量";
    public string Category => "尺寸测量";
    public string Description => "沿指定测量区域搜索边缘并输出测量结果。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<double>(FlowParameterKeys.CaliperWidth, displayName: "卡尺宽度", defaultValue: 20.0, minimum: 1.0, smallChange: 1.0, decimalPlaces: 0),
        KwyParameterDefinition.Create<double>(FlowParameterKeys.EdgeThreshold, displayName: "边缘阈值", defaultValue: 30.0, minimum: 0.0, maximum: 255.0, smallChange: 1.0, decimalPlaces: 0),
        KwyParameterDefinition.Create<string>(
            FlowParameterKeys.EdgePolarity,
            displayName: "极性",
            defaultValue: VisionEdgePolarity.All.ToString(),
            inputType: InputType.ComboBox,
            itemsSource: Enum.GetNames<VisionEdgePolarity>()),
        .. VisionRoiParameterDefinitions.Create()
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowPortNames.ImageInput, Direction = PortDirection.Input, DataType = PortDataTypes.Image }],
        OutputPorts = [new FlowPort { Name = FlowPortNames.Point, Direction = PortDirection.Output, DataType = PortDataTypes.Point }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class LineFittingDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.VisionLineFitting;
    public string DisplayName => "直线拟合";
    public string Category => "尺寸测量";
    public string Description => "根据点集或边缘点拟合直线。";
    public string? IconKey => null;

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowPortNames.Points, Direction = PortDirection.Input, DataType = PortDataTypes.Point, AllowMultiple = true }],
        OutputPorts = [new FlowPort { Name = FlowPortNames.Line, Direction = PortDirection.Output, DataType = PortDataTypes.Line }]
    };
}

public sealed class CircleFittingDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.VisionCircleFitting;
    public string DisplayName => "圆拟合";
    public string Category => "尺寸测量";
    public string Description => "根据点集拟合圆。";
    public string? IconKey => null;

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowPortNames.Points, Direction = PortDirection.Input, DataType = PortDataTypes.Point, AllowMultiple = true }],
        OutputPorts = [new FlowPort { Name = FlowPortNames.Circle, Direction = PortDirection.Output, DataType = PortDataTypes.Circle }]
    };
}

public sealed class TemplateMatchingDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.VisionTemplateMatching;
    public string DisplayName => "模板匹配";
    public string Category => "定位对位";
    public string Description => "使用形状模板或灰度模板进行目标定位。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<string>(FlowParameterKeys.TemplateId, displayName: "模板名称", defaultValue: string.Empty, isRequired: true),
        KwyParameterDefinition.Create<double>(FlowParameterKeys.MinScore, displayName: "最小分数", defaultValue: 0.75, minimum: 0.0, maximum: 1.0, smallChange: 0.05, decimalPlaces: 3),
        .. VisionRoiParameterDefinitions.Create()
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowPortNames.ImageInput, Direction = PortDirection.Input, DataType = PortDataTypes.Image }],
        OutputPorts = [new FlowPort { Name = FlowPortNames.MatchResult, Direction = PortDirection.Output, DataType = PortDataTypes.MatchResult }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}

public sealed class YoloObjectDetectionDescriptor : IFlowNodeDescriptor
{
    public string NodeType => FlowNodeTypes.VisionYoloObjectDetection;
    public string DisplayName => "YOLO 目标检测";
    public string Category => "深度学习";
    public string Description => "使用已注册的 YOLO/检测模型进行目标检测。";
    public string? IconKey => null;

    public IReadOnlyList<KwyParameterDefinition> Parameters =>
    [
        KwyParameterDefinition.Create<string>(FlowParameterKeys.ModelId, displayName: "模型名称", defaultValue: string.Empty, isRequired: true),
        KwyParameterDefinition.Create<double>(FlowParameterKeys.MinScore, displayName: "最小分数", defaultValue: 0.5, minimum: 0.0, maximum: 1.0, smallChange: 0.05, decimalPlaces: 3),
        KwyParameterDefinition.Create<string>(FlowParameterKeys.ClassFilter, displayName: "类别过滤", defaultValue: "*", description: "使用 * 表示不过滤；多个类别用逗号分隔。")
    ];

    public FlowNode CreateDefaultNode() => new()
    {
        NodeType = NodeType,
        DisplayName = DisplayName,
        InputPorts = [new FlowPort { Name = FlowPortNames.ImageInput, Direction = PortDirection.Input, DataType = PortDataTypes.Image }],
        OutputPorts = [new FlowPort { Name = FlowPortNames.DetectionResult, Direction = PortDirection.Output, DataType = PortDataTypes.MatchResult }],
        Parameters = Parameters.ToDictionary(item => item.Key, item => item.DefaultValue)
    };
}
