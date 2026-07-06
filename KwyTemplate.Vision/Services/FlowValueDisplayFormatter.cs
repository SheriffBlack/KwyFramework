using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.DeepLearning;
using KwyTemplate.Vision.Executors;
using System.Collections;
using System.Globalization;

namespace KwyTemplate.Vision.Services;

internal static class FlowValueDisplayFormatter
{
    public static string FormatFlowValue(FlowValue value)
        => !value.HasValue ? "<Missing>" : FormatValue(value.Value);

    public static string FormatValue(object? value)
        => value switch
        {
            null => "N/A",
            string text => text,
            bool boolean => boolean ? "True" : "False",
            double number => number.ToString("G4", CultureInfo.CurrentCulture),
            float number => number.ToString("G4", CultureInfo.CurrentCulture),
            decimal number => number.ToString("G4", CultureInfo.CurrentCulture),
            IVisionImage image => FormatImage(image),
            IEnumerable<IVisionImage> images => FormatImages(images),
            ObjectDetectionResult detections => FormatDetections(detections),
            FlowValue flowValue => FormatFlowValue(flowValue),
            IEnumerable collection when value is not string => FormatCollection(collection),
            IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };

    private static string FormatImage(IVisionImage image)
        => $"\u56fe\u50cf {image.Width}x{image.Height} {image.PixelFormat}";

    private static string FormatImages(IEnumerable<IVisionImage> images)
    {
        if (images is IReadOnlyCollection<IVisionImage> readOnlyCollection)
        {
            return $"\u56fe\u50cf\u96c6\u5408 {readOnlyCollection.Count} \u5f20";
        }

        if (images is ICollection collection)
        {
            return $"\u56fe\u50cf\u96c6\u5408 {collection.Count} \u5f20";
        }

        return "\u56fe\u50cf\u96c6\u5408";
    }

    public static string FormatDetections(ObjectDetectionResult result)
    {
        if (result.Detections.Count == 0)
        {
            return "检测 0 个目标";
        }

        ObjectDetection best = result.Detections
            .OrderByDescending(item => item.Confidence)
            .First();
        string labels = string.Join(
            "/",
            result.Detections
                .GroupBy(item => item.Label)
                .OrderByDescending(item => item.Count())
                .Take(3)
                .Select(item => $"{item.Key}x{item.Count()}"));

        return $"检测 {result.Detections.Count} 个目标，最高 {best.Label} {best.Confidence:P0}，{labels}";
    }

    private static string FormatCollection(IEnumerable collection)
    {
        if (collection is ICollection countable)
        {
            return $"\u96c6\u5408 {countable.Count} \u9879";
        }

        return "\u96c6\u5408";
    }
}
