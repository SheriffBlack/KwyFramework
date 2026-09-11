using Kwy.ComponentModel;

namespace KwyTemplate.Vision.NodeDescriptors;

internal static class VisionRoiParameterDefinitions
{
    public const string RoiKind = "RoiKind";
    public const string RoiX = "RoiX";
    public const string RoiY = "RoiY";
    public const string RoiWidth = "RoiWidth";
    public const string RoiHeight = "RoiHeight";
    public const string RoiAngle = "RoiAngle";
    public const string RoiRadius = "RoiRadius";

    public static IReadOnlyList<KwyParameterDefinition> Create()
        =>
        [
            KwyParameterDefinition.Create<VisionRoiKind>(
                RoiKind,
                displayName: "ROI 类型",
                defaultValue: VisionRoiKind.Rectangle,
                category: "ROI",
                description: "选择算法搜索区域的形状。"),
            KwyParameterDefinition.Create<double>(
                RoiX,
                displayName: "ROI X",
                defaultValue: 0.0,
                category: "ROI",
                description: "矩形左上角 X，或圆/旋转矩形中心 X。",
                smallChange: 1.0,
                decimalPlaces: 1),
            KwyParameterDefinition.Create<double>(
                RoiY,
                displayName: "ROI Y",
                defaultValue: 0.0,
                category: "ROI",
                description: "矩形左上角 Y，或圆/旋转矩形中心 Y。",
                smallChange: 1.0,
                decimalPlaces: 1),
            KwyParameterDefinition.Create<double>(
                RoiWidth,
                displayName: "ROI 宽度",
                defaultValue: 0.0,
                category: "ROI",
                description: "0 表示使用整幅图像；圆 ROI 可忽略该值。",
                minimum: 0.0,
                smallChange: 1.0,
                decimalPlaces: 1),
            KwyParameterDefinition.Create<double>(
                RoiHeight,
                displayName: "ROI 高度",
                defaultValue: 0.0,
                category: "ROI",
                description: "0 表示使用整幅图像；圆 ROI 可忽略该值。",
                minimum: 0.0,
                smallChange: 1.0,
                decimalPlaces: 1),
            KwyParameterDefinition.Create<double>(
                RoiAngle,
                displayName: "ROI 角度",
                defaultValue: 0.0,
                category: "ROI",
                description: "旋转矩形角度，单位为度。",
                minimum: -360.0,
                maximum: 360.0,
                smallChange: 1.0,
                decimalPlaces: 1),
            KwyParameterDefinition.Create<double>(
                RoiRadius,
                displayName: "ROI 半径",
                defaultValue: 0.0,
                category: "ROI",
                description: "圆形 ROI 半径，0 表示使用宽高较小值的一半。",
                minimum: 0.0,
                smallChange: 1.0,
                decimalPlaces: 1)
        ];
}
