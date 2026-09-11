using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.DeepLearning;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.WPF.Sources;
using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.NodeDescriptors;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace KwyTemplate.Vision.Executors;

public sealed class VisionLocalImageInputExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionLocalImage;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string path = GetParameter(node, FlowParameterKeys.ImagePath, string.Empty);
        IVisionFrameSource frameSource = GetRequiredService<IVisionFrameSourceFactory>().CreateLocalImageSource(path);
        var frames = new List<VisionFrame>();
        await foreach (VisionFrame frame in frameSource.ReadAllFramesAsync(ct).ConfigureAwait(false))
        {
            frames.Add(frame);
        }

        if (frames.Count == 0)
        {
            return FlowNodeExecutionResult.Failed("请先选择本地图像文件、多个图像文件或图像文件夹。");
        }

        IReadOnlyList<IVisionImage> images = frames.Select(frame => frame.Image).ToArray();
        if (context.Items.TryGetValue(FlowExecutionContext.BatchCurrentImageKey, out object? batchImage)
            && batchImage is IVisionImage currentImage)
        {
            images = [currentImage];
        }

        return FlowNodeExecutionResult.Ok(new Dictionary<string, FlowValue>
        {
            [FlowPortNames.Image] = FlowValue.From(images[0], PortDataTypes.Image),
            [FlowPortNames.Images] = FlowValue.From(images, PortDataTypes.ImageList)
        });
    }
}

public sealed class VisionLocalVideoInputExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionLocalVideo;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string videoPath = GetParameter(node, FlowParameterKeys.VideoPath, string.Empty);
        int frameIndex = Math.Max(0, GetParameter(node, FlowParameterKeys.FrameIndex, 0));
        IVisionFrameSource frameSource = GetRequiredService<IVisionFrameSourceFactory>().CreateLocalVideoSource(videoPath);
        if (!frameSource.IsConfigured)
        {
            return FlowNodeExecutionResult.Failed("请先选择本地视频文件。");
        }

        VisionFrame? frame = await frameSource.ReadFrameAsync(frameIndex, ct).ConfigureAwait(false);
        if (frame == null)
        {
            return FlowNodeExecutionResult.Failed($"未能从视频读取图像帧：{videoPath}");
        }

        return FlowNodeExecutionResult.Ok(new Dictionary<string, FlowValue>
        {
            [FlowPortNames.Image] = FlowValue.From(frame.Image, PortDataTypes.Image)
        });
    }
}

public sealed class VisionCameraCaptureInputExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionCameraCapture;

    public override Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string cameraName = GetParameter(node, FlowParameterKeys.CameraName, string.Empty);
        string triggerMode = GetParameter(node, FlowParameterKeys.TriggerMode, "Continuous");
        double exposureMs = GetParameter(node, FlowParameterKeys.ExposureMs, 10.0);
        double gain = GetParameter(node, FlowParameterKeys.Gain, 1.0);
        IVisionFrameSource frameSource = GetRequiredService<IVisionFrameSourceFactory>().CreateCameraSource(new VisionCameraSourceOptions
        {
            CameraName = cameraName,
            TriggerMode = triggerMode,
            ExposureMs = exposureMs,
            Gain = gain
        });

        return Task.FromResult(FlowNodeExecutionResult.Failed(
            !frameSource.IsConfigured
                ? "请填写相机名称或本机摄像头索引。"
                : $"相机帧源尚未接入真实设备服务：{frameSource.DisplayName}"));
    }
}

public sealed class VisionImagePreprocessExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionImagePreprocess;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        IVisionImage image = GetFirstInput<IVisionImage>(inputs)
            ?? throw new InvalidOperationException("图像预处理需要图像输入。");
        VisionPreprocessOperation operation = GetParameter(node, FlowParameterKeys.Operation, VisionPreprocessOperation.Mean);
        double radius = GetParameter(node, FlowParameterKeys.Radius, 3.0);
        int mask = Math.Max(1, (int)Math.Round(radius));

        IVisionAlgorithm<ImagePreprocessRequest, ImagePreprocessResult> algorithm =
            GetAlgorithm<ImagePreprocessRequest, ImagePreprocessResult>("ImagePreprocess");

        var result = await algorithm
            .ExecuteAsync(new ImagePreprocessRequest(image, operation, mask, mask, Radius: radius, Region: GetRoiRegion(node)), ct)
            .ConfigureAwait(false);

        return result.Succeeded && result.Value != null
            ? OkToFirstOutput(node, result.Value.Image, PortDataTypes.Image, result.Overlays)
            : FlowNodeExecutionResult.Failed(result.ErrorMessage ?? "图像预处理失败。");
    }
}

public sealed class VisionThresholdExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionThreshold;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        IVisionImage image = GetFirstInput<IVisionImage>(inputs)
            ?? throw new InvalidOperationException("阈值分割需要图像输入。");
        double lower = GetParameter(node, FlowParameterKeys.ThresholdLower, 128.0);
        double upper = GetParameter(node, FlowParameterKeys.ThresholdUpper, 255.0);

        IVisionAlgorithm<BlobInspectionRequest, BlobInspectionResult> algorithm =
            GetAlgorithm<BlobInspectionRequest, BlobInspectionResult>("BlobInspection");

        var result = await algorithm
            .ExecuteAsync(new BlobInspectionRequest(image, lower, upper, 0, double.MaxValue, GetRoiRegion(node)), ct)
            .ConfigureAwait(false);

        return result.Succeeded && result.Value != null
            ? OkToFirstOutput(node, result.Value, PortDataTypes.Region, result.Overlays)
            : FlowNodeExecutionResult.Failed(result.ErrorMessage ?? "阈值分割失败。");
    }
}

public sealed class VisionBlobExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionBlob;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        IVisionImage image = GetFirstInput<IVisionImage>(inputs)
            ?? throw new InvalidOperationException("Blob 分析需要图像输入。");
        double minArea = GetParameter(node, FlowParameterKeys.MinArea, 10.0);
        double maxArea = GetParameter(node, FlowParameterKeys.MaxArea, double.MaxValue);

        IVisionAlgorithm<BlobInspectionRequest, BlobInspectionResult> algorithm =
            GetAlgorithm<BlobInspectionRequest, BlobInspectionResult>("BlobInspection");

        var result = await algorithm
            .ExecuteAsync(new BlobInspectionRequest(image, 0, 255, minArea, maxArea, GetRoiRegion(node)), ct)
            .ConfigureAwait(false);

        return result.Succeeded && result.Value != null
            ? OkToFirstOutput(node, result.Value.Blobs, PortDataTypes.BlobList, result.Overlays)
            : FlowNodeExecutionResult.Failed(result.ErrorMessage ?? "Blob 分析失败。");
    }
}

public sealed class VisionCaliperExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionCaliper;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        IVisionImage image = GetFirstInput<IVisionImage>(inputs)
            ?? throw new InvalidOperationException("卡尺测量需要图像输入。");
        double width = GetParameter(node, FlowParameterKeys.CaliperWidth, 20.0);
        double threshold = GetParameter(node, FlowParameterKeys.EdgeThreshold, 30.0);
        VisionEdgePolarity polarity = GetParameter(node, FlowParameterKeys.EdgePolarity, VisionEdgePolarity.All);

        VisionRotatedRectangle measureRegion = TryGetRoiRectangle(node, out VisionRectangle roi)
            ? new VisionRotatedRectangle(
                new VisionPoint(roi.X + roi.Width / 2.0, roi.Y + roi.Height / 2.0),
                Math.Max(1, roi.Width),
                Math.Max(1, roi.Height),
                0)
            : new VisionRotatedRectangle(
                new VisionPoint(image.Width / 2.0, image.Height / 2.0),
                Math.Max(1, image.Width),
                Math.Max(1, width),
                0);

        var caliper = new CaliperDefinition("Caliper 1", measureRegion);
        IVisionAlgorithm<CaliperGroupMeasurementRequest, CaliperGroupMeasurementResult> algorithm =
            GetAlgorithm<CaliperGroupMeasurementRequest, CaliperGroupMeasurementResult>("CaliperGroupMeasurement");

        var result = await algorithm
            .ExecuteAsync(new CaliperGroupMeasurementRequest(image, [caliper], 1.0, threshold, polarity), ct)
            .ConfigureAwait(false);

        return result.Succeeded && result.Value != null
            ? OkToFirstOutput(node, result.Value, PortDataTypes.Point, result.Overlays)
            : FlowNodeExecutionResult.Failed(result.ErrorMessage ?? "卡尺测量失败。");
    }
}

public sealed class VisionLineFittingExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionLineFitting;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        IReadOnlyList<VisionPoint> points = CollectPoints(inputs);
        if (points.Count < 2)
        {
            return FlowNodeExecutionResult.Failed("直线拟合至少需要 2 个点。");
        }

        IVisionAlgorithm<LineFittingRequest, LineFittingResult> algorithm =
            GetAlgorithm<LineFittingRequest, LineFittingResult>("LineFitting");

        var result = await algorithm.ExecuteAsync(new LineFittingRequest(points), ct).ConfigureAwait(false);
        return result.Succeeded && result.Value != null
            ? OkToFirstOutput(node, result.Value.Line, PortDataTypes.Line, result.Overlays)
            : FlowNodeExecutionResult.Failed(result.ErrorMessage ?? "直线拟合失败。");
    }
}

public sealed class VisionCircleFittingExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionCircleFitting;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        IReadOnlyList<VisionPoint> points = CollectPoints(inputs);
        if (points.Count < 3)
        {
            return FlowNodeExecutionResult.Failed("圆拟合至少需要 3 个点。");
        }

        IVisionAlgorithm<CircleFittingRequest, CircleFittingResult> algorithm =
            GetAlgorithm<CircleFittingRequest, CircleFittingResult>("CircleFitting");

        var result = await algorithm.ExecuteAsync(new CircleFittingRequest(points), ct).ConfigureAwait(false);
        return result.Succeeded && result.Value != null
            ? OkToFirstOutput(node, result.Value.Circle, PortDataTypes.Circle, result.Overlays)
            : FlowNodeExecutionResult.Failed(result.ErrorMessage ?? "圆拟合失败。");
    }
}

public sealed class VisionTemplateMatchingExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionTemplateMatching;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        IVisionImage image = GetFirstInput<IVisionImage>(inputs)
            ?? throw new InvalidOperationException("模板匹配需要图像输入。");
        string templateId = GetParameter(node, FlowParameterKeys.TemplateId, string.Empty);
        double minScore = GetParameter(node, FlowParameterKeys.MinScore, 0.75);
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return FlowNodeExecutionResult.Failed("请先填写模板名称。");
        }

        IVisionAlgorithm<ShapeMatchingRequest, ShapeMatchingResult> algorithm =
            GetAlgorithm<ShapeMatchingRequest, ShapeMatchingResult>("ShapeMatching");

        var result = await algorithm
            .ExecuteAsync(new ShapeMatchingRequest(image, templateId, -Math.PI, Math.PI * 2, minScore, SearchRegion: GetRoiRegion(node)), ct)
            .ConfigureAwait(false);

        return result.Succeeded && result.Value != null
            ? OkToFirstOutput(node, result.Value.Matches, PortDataTypes.MatchResult, result.Overlays)
            : FlowNodeExecutionResult.Failed(result.ErrorMessage ?? "模板匹配失败。");
    }
}

public sealed class VisionYoloObjectDetectionExecutor(IServiceProvider serviceProvider)
    : VisionAlgorithmExecutorBase(serviceProvider)
{
    public override string NodeType => FlowNodeTypes.VisionYoloObjectDetection;

    public override async Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default)
    {
        IVisionImage image = GetFirstInput<IVisionImage>(inputs)
            ?? throw new InvalidOperationException("YOLO 目标检测需要图像输入。");
        string modelId = GetParameter(node, FlowParameterKeys.ModelId, string.Empty);
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return FlowNodeExecutionResult.Failed("请先填写模型名称。");
        }

        double minScore = GetParameter(node, FlowParameterKeys.MinScore, 0.5);
        string classFilter = GetParameter(node, FlowParameterKeys.ClassFilter, "*");
        HashSet<string> allowedLabels = ParseClassFilter(classFilter);

        IVisionModelRegistry registry = GetRequiredService<IVisionModelRegistry>();
        if (!registry.Models.Any(item => string.Equals(item.ModelId, modelId, StringComparison.OrdinalIgnoreCase)))
        {
            string registeredModels = registry.Models.Count == 0
                ? "无"
                : string.Join(", ", registry.Models.Select(item => item.ModelId));
            return FlowNodeExecutionResult.Failed(
                $"未找到 YOLO 模型：{modelId}。已注册模型：{registeredModels}。请在视觉模型服务中注册 IVisionModel<IVisionImage, ObjectDetectionResult>。");
        }

        IVisionModel<IVisionImage, ObjectDetectionResult> model = registry.GetRequired<IVisionImage, ObjectDetectionResult>(modelId);
        if (model.State != VisionModelState.Loaded)
        {
            await model.LoadAsync(ct).ConfigureAwait(false);
        }

        ObjectDetectionResult raw = await model.PredictAsync(image, ct).ConfigureAwait(false);
        ObjectDetection[] detections = raw.Detections
            .Where(item => item.Confidence >= minScore)
            .Where(item => allowedLabels.Count == 0 || allowedLabels.Contains(item.Label))
            .ToArray();

        var filtered = new ObjectDetectionResult(detections);
        var overlays = new List<IVisionOverlayShape>(detections.Length * 2);
        foreach (ObjectDetection item in detections)
        {
            string label = $"{item.Label} {item.Confidence:P0}";
            overlays.Add(new OverlayRectangle(item.Bounds, VisionColor.Green, 2, label));
            overlays.Add(new OverlayText(
                new VisionPoint(item.Bounds.X, Math.Max(0, item.Bounds.Y - 18)),
                label,
                VisionColor.Green,
                14,
                1,
                label));
        }

        return OkToFirstOutput(node, filtered, PortDataTypes.MatchResult, overlays);
    }

    private static HashSet<string> ParseClassFilter(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "*")
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

public abstract class VisionAlgorithmExecutorBase : FlowNodeExecutorBase
{
    private readonly IServiceProvider serviceProvider;

    protected VisionAlgorithmExecutorBase(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    protected TService GetRequiredService<TService>()
        where TService : notnull
        => serviceProvider.GetRequiredService<TService>();

    protected IVisionAlgorithm<TRequest, TResult> GetAlgorithm<TRequest, TResult>(string algorithmId)
    {
        IVisionAlgorithmRegistry registry = serviceProvider.GetRequiredService<IVisionAlgorithmRegistry>();
        return registry.GetRequired<TRequest, TResult>(algorithmId);
    }

    protected static T? GetFirstInput<T>(IReadOnlyDictionary<string, FlowValue> inputs)
        where T : class
        => inputs.Values.FirstOrDefault(item => item.HasValue && item.Value is T)?.Value as T;

    protected static T GetParameter<T>(FlowNode node, string key, T defaultValue)
    {
        if (!node.Parameters.TryGetValue(key, out object? value) || value == null)
        {
            return defaultValue;
        }

        return ConvertValue(value, defaultValue);
    }

    protected static FlowNodeExecutionResult OkToFirstOutput(
        FlowNode node,
        object? value,
        string? dataType = null,
        IReadOnlyList<IVisionOverlayShape>? overlays = null)
    {
        string outputName = node.OutputPorts.FirstOrDefault()?.Name ?? FlowPortNames.Output;
        return FlowNodeExecutionResult.Ok(new Dictionary<string, FlowValue>
        {
            [outputName] = FlowValue.From(value, dataType)
        }, overlays);
    }

    protected static IReadOnlyList<VisionPoint> CollectPoints(IReadOnlyDictionary<string, FlowValue> inputs)
    {
        var points = new List<VisionPoint>();
        foreach (FlowValue input in inputs.Values)
        {
            if (!input.HasValue || input.Value is null)
            {
                continue;
            }

            switch (input.Value)
            {
                case VisionPoint point:
                    points.Add(point);
                    break;
                case IEnumerable<VisionPoint> pointList:
                    points.AddRange(pointList);
                    break;
            }
        }

        return points;
    }

    protected static IVisionRegion? GetRoiRegion(FlowNode node)
    {
        if (!TryGetRoiRectangle(node, out VisionRectangle rectangle))
        {
            return null;
        }

        VisionRoiKind kind = GetParameter(node, VisionRoiParameterDefinitions.RoiKind, VisionRoiKind.Rectangle);
        return kind switch
        {
            VisionRoiKind.Circle => new CircleRegion(new VisionCircle(
                new VisionPoint(rectangle.X, rectangle.Y),
                ResolveCircleRadius(node, rectangle))),
            VisionRoiKind.RotatedRectangle => new RotatedRectangleRegion(new VisionRotatedRectangle(
                new VisionPoint(rectangle.X, rectangle.Y),
                rectangle.Width,
                rectangle.Height,
                DegreesToRadians(GetParameter(node, VisionRoiParameterDefinitions.RoiAngle, 0.0)))),
            _ => new RectangleRegion(rectangle)
        };
    }

    protected static bool TryGetRoiRectangle(FlowNode node, out VisionRectangle rectangle)
    {
        rectangle = default;
        if (!TryGetParameterDouble(node, VisionRoiParameterDefinitions.RoiX, out double x)
            || !TryGetParameterDouble(node, VisionRoiParameterDefinitions.RoiY, out double y))
        {
            return false;
        }

        VisionRoiKind kind = GetParameter(node, VisionRoiParameterDefinitions.RoiKind, VisionRoiKind.Rectangle);
        double width = 0;
        double height = 0;
        if (kind == VisionRoiKind.Circle
            && TryGetParameterDouble(node, VisionRoiParameterDefinitions.RoiRadius, out double radius)
            && radius > 0)
        {
            width = radius * 2.0;
            height = radius * 2.0;
        }
        else if (!TryGetParameterDouble(node, VisionRoiParameterDefinitions.RoiWidth, out width)
            || !TryGetParameterDouble(node, VisionRoiParameterDefinitions.RoiHeight, out height)
            || width <= 0
            || height <= 0)
        {
            return false;
        }

        rectangle = new VisionRectangle(x, y, width, height);
        return true;
    }

    private static bool TryGetParameterDouble(FlowNode node, string key, out double value)
    {
        value = 0;
        if (!node.Parameters.TryGetValue(key, out object? raw) || raw == null)
        {
            return false;
        }

        double converted = ConvertValue(raw, double.NaN);
        if (double.IsNaN(converted))
        {
            return false;
        }

        value = converted;
        return true;
    }

    private static double ResolveCircleRadius(FlowNode node, VisionRectangle rectangle)
    {
        double radius = GetParameter(node, VisionRoiParameterDefinitions.RoiRadius, 0.0);
        return radius > 0
            ? radius
            : Math.Max(1.0, Math.Min(rectangle.Width, rectangle.Height) / 2.0);
    }

    private static double DegreesToRadians(double degrees)
        => degrees * Math.PI / 180.0;

    private static T ConvertValue<T>(object raw, T defaultValue)
    {
        try
        {
            if (raw is JsonElement element)
            {
                raw = element.ValueKind switch
                {
                    JsonValueKind.Number when typeof(T) == typeof(int) => element.GetInt32(),
                    JsonValueKind.Number => element.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => element.GetString() ?? string.Empty,
                    _ => raw
                };
            }

            Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType.IsEnum)
            {
                if (raw is string text)
                {
                    return (T)Enum.Parse(targetType, text, ignoreCase: true);
                }

                return (T)Enum.ToObject(targetType, raw);
            }

            return (T)Convert.ChangeType(raw, targetType);
        }
        catch
        {
            return defaultValue;
        }
    }
}
