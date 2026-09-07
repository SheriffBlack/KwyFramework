using System.Globalization;
using System.Text;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.MES.Cyntec;

internal static class CyntecStandardSampleCheckFileWriter
{
    public static void Write(string filePath, MesStandardSampleCheckSaveRequest request)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var lines = new List<string>(request.Measurements.Count);
        for (int i = 0; i < request.Measurements.Count; i++)
        {
            MesMeasurementResult measurement = request.Measurements[i];
            lines.Add(FormatLine(i + 1, request.SampleCode, measurement));
        }

        File.WriteAllLines(filePath, lines, new UTF8Encoding(false));
    }

    private static string FormatLine(int index, string sampleCode, MesMeasurementResult measurement)
    {
        string resultText = measurement.Passed ? "OK" : "NG";
        string valueText = measurement.Value.ToString("0.####", CultureInfo.InvariantCulture);
        // Keep the legacy UploadStr layout:
        // correction type unit (source ItemName), fixed source number, correction type (source MeterType).
        string correctionTypeUnit = string.IsNullOrWhiteSpace(measurement.ItemName)
            ? measurement.DisplayName
            : measurement.ItemName;
        string correctionType = string.IsNullOrWhiteSpace(measurement.MeterType)
            ? measurement.ParameterId
            : measurement.MeterType;
        string frequency = string.IsNullOrWhiteSpace(measurement.Frequency) ? "0" : measurement.Frequency;
        string frequencyUnit = string.IsNullOrWhiteSpace(measurement.FrequencyUnit) ? "0" : measurement.FrequencyUnit;

        return string.Join(",", new[]
        {
            Csv(index.ToString(CultureInfo.InvariantCulture)),
            Csv(measurement.SampleId ?? sampleCode),
            Csv(ToUpperToken(correctionTypeUnit)),
            // Cyntec's equipment check-file contract uses a fixed "1" here.
            // The standard-part source field (for example "01") is not emitted.
            Csv("1"),
            Csv(ToUpperToken(correctionType)),
            Csv(frequency),
            Csv(ToUpperToken(frequencyUnit)),
            Csv(valueText),
            Csv(ToUpperToken(resultText))
        });
    }

    private static string ToUpperToken(string value)
        => value.ToUpperInvariant();

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
