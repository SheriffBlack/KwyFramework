namespace KwyTemplate.Vision.NodeDescriptors;

internal static class FlowNodeTypes
{
    public const string MathNumberConstant = "Math.NumberConstant";
    public const string MathAdd = "Math.Add";
    public const string LogicRangeJudgement = "Logic.RangeJudgement";
    public const string LogicDelay = "Logic.Delay";
    public const string IoReadDigitalInput = "IO.ReadDigitalInput";
    public const string IoWriteDigitalOutput = "IO.WriteDigitalOutput";
    public const string VisionLocalImage = "Vision.Input.LocalImage";
    public const string VisionLocalVideo = "Vision.Input.LocalVideo";
    public const string VisionCameraCapture = "Vision.Input.CameraCapture";
    public const string VisionImagePreprocess = "Vision.ImagePreprocess";
    public const string VisionThreshold = "Vision.Threshold";
    public const string VisionBlob = "Vision.Blob";
    public const string VisionCaliper = "Vision.Caliper";
    public const string VisionLineFitting = "Vision.LineFitting";
    public const string VisionCircleFitting = "Vision.CircleFitting";
    public const string VisionTemplateMatching = "Vision.TemplateMatching";
    public const string VisionYoloObjectDetection = "Vision.ObjectDetection.Yolo";
}

internal static class FlowParameterKeys
{
    public const string Value = "Value";
    public const string ValueA = "ValueA";
    public const string ValueB = "ValueB";
    public const string Minimum = "Minimum";
    public const string Maximum = "Maximum";
    public const string DelayMs = "DelayMs";
    public const string DeviceName = "DeviceName";
    public const string Channel = "Channel";
    public const string ImagePath = "ImagePath";
    public const string VideoPath = "VideoPath";
    public const string FrameIndex = "FrameIndex";
    public const string CameraName = "CameraName";
    public const string TriggerMode = "TriggerMode";
    public const string ExposureMs = "ExposureMs";
    public const string Gain = "Gain";
    public const string Operation = "Operation";
    public const string Radius = "Radius";
    public const string ThresholdLower = "ThresholdLower";
    public const string ThresholdUpper = "ThresholdUpper";
    public const string MinArea = "MinArea";
    public const string MaxArea = "MaxArea";
    public const string CaliperWidth = "CaliperWidth";
    public const string EdgeThreshold = "EdgeThreshold";
    public const string EdgePolarity = "EdgePolarity";
    public const string TemplateId = "TemplateId";
    public const string MinScore = "MinScore";
    public const string ModelId = "ModelId";
    public const string ClassFilter = "ClassFilter";
}

internal static class FlowPortNames
{
    public const string Input = "输入";
    public const string Output = "输出";
    public const string Image = "图像";
    public const string Images = "图像集合";
    public const string ImageInput = "图像输入";
    public const string ImageOutput = "图像输出";
    public const string Region = "区域";
    public const string Blobs = "Blob集合";
    public const string Point = "点";
    public const string Points = "点集";
    public const string Line = "直线";
    public const string Circle = "圆";
    public const string MatchResult = "匹配结果";
    public const string DetectionResult = "检测结果";
    public const string Signal = "信号";
    public const string Result = "结果";
    public const string Ok = "OK";
}
