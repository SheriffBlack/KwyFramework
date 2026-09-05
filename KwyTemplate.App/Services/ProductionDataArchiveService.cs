using System.IO;
using System.Text;
using System.Globalization;
using KwyTemplate.App.Models;
using KwyTemplate.Contracts.Localization;

namespace KwyTemplate.App.Services;

public interface IProductionDataArchiveService
{
    Task AppendCheckRecordAsync(DateTimeOffset time, string workOrderNo, IReadOnlyList<string> fields, CancellationToken cancellationToken = default);
    Task AppendProductionRecordAsync(ProductionDataArchiveRequest request, CancellationToken cancellationToken = default);
}

public sealed record ProductionDataArchiveRequest(
    DateTimeOffset Time,
    string WorkOrderNo,
    string OperatorNo,
    string MachineId,
    string SourceFilePath,
    IReadOnlyList<ProductionDataMeasurementDefinition> Measurements);

public sealed record ProductionDataMeasurementDefinition(
    string Name,
    string Unit,
    string CenterValue,
    double? UpperLimit,
    double? LowerLimit,
    bool IsEnabled);

/// <summary>本地可读归档；不改变 MES Equipment、Output 的既有文件格式。</summary>
public sealed class ProductionDataArchiveService : IProductionDataArchiveService
{
    private readonly ProductionDataOptionsStore optionsStore;
    private readonly ILocalizationService localizationService;
    private readonly SemaphoreSlim fileGate = new(1, 1);

    public ProductionDataArchiveService(ProductionDataOptionsStore optionsStore, ILocalizationService localizationService)
    {
        this.optionsStore = optionsStore;
        this.localizationService = localizationService;
    }

    public async Task AppendCheckRecordAsync(DateTimeOffset time, string workOrderNo, IReadOnlyList<string> fields, CancellationToken cancellationToken = default)
    {
        string[] headers =
        [
            T("ProductionData.Check.Date", "日期"), T("ProductionData.Check.Time", "时间"), T("ProductionData.Check.WorkOrder", "工单号"),
            T("ProductionData.Check.StandardCode", "标准件编号"), T("ProductionData.Check.StandardLs", "标准件LS"), T("ProductionData.Check.StandardRs", "标准件RS"), T("ProductionData.Check.StandardDcr", "标准件DCR"), T("ProductionData.Check.StandardHighLs", "标准件高频LS"), T("ProductionData.Check.StandardHighRs", "标准件高频RS"), T("ProductionData.Check.StandardHighQ", "标准件高频Q"),
            T("ProductionData.Check.ConfirmCode", "确认件编号"), T("ProductionData.Check.ConfirmLs", "确认件LS"), T("ProductionData.Check.ConfirmRs", "确认件RS"), T("ProductionData.Check.ConfirmDcr", "确认件DCR"), T("ProductionData.Check.ConfirmHighLs", "确认件高频LS"), T("ProductionData.Check.ConfirmHighRs", "确认件高频RS"), T("ProductionData.Check.ConfirmHighQ", "确认件高频Q"),
            T("ProductionData.Check.Polarity1Forward", "极性1正向"), T("ProductionData.Check.Polarity1Reverse", "极性1反向"), T("ProductionData.Check.Polarity2Forward", "极性2正向"), T("ProductionData.Check.Polarity2Reverse", "极性2反向")
        ];
        string[] row = [time.ToString("yyyy-MM-dd"), time.ToString("HH:mm:ss"), workOrderNo, .. fields];
        await AppendCsvRowAsync(BuildSampleDataPath(workOrderNo, time), headers, row, cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendProductionRecordAsync(ProductionDataArchiveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.SourceFilePath))
        {
            return;
        }

        string content = await File.ReadAllTextAsync(request.SourceFilePath, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ProductionDataRow> rows = ParseProductionRows(content, request.MachineId);
        string targetPath = BuildProductionRecordPath(request.WorkOrderNo, request.OperatorNo, request.Time);
        await fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, BuildProductionReport(rows, request.Measurements), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            fileGate.Release();
        }
    }

    private static string BuildProductionReport(IReadOnlyList<ProductionDataRow> rows, IReadOnlyList<ProductionDataMeasurementDefinition> definitions)
    {
        ProductionDataMeasurementDefinition dcr = FindDefinition(definitions, "Dcr");
        ProductionDataMeasurementDefinition ls = FindDefinition(definitions, "Ls");
        ProductionDataMeasurementDefinition rs = FindDefinition(definitions, "Rs");
        ProductionDataMeasurementDefinition highLs = FindDefinition(definitions, "H_Ls");
        ProductionDataMeasurementDefinition highQ = FindDefinition(definitions, "H_Q");
        var text = new StringBuilder();
        text.AppendLine($"M:{rows.Count.ToString(CultureInfo.InvariantCulture)}");
        text.AppendLine();
        AppendSummary(text, "Dcr", dcr, rows.Select(static row => row.Dcr));
        text.AppendLine();
        AppendSummary(text, "Ls", ls, rows.Select(static row => row.Ls));
        text.AppendLine();
        AppendSummary(text, "Rs", rs, rows.Select(static row => row.Rs));
        text.AppendLine();
        string[] headers =
        [
            "No", Header("Dcr", dcr), Header("Ls", ls), Header("Rs", rs), Header("H_Ls", highLs), Header("H_Q", highQ),
            "Dcr-R", "Ls-R", "Rs-R", "H_Ls-R", "H_Q-R", "Time"
        ];
        text.AppendLine(string.Join(',', headers));

        foreach (ProductionDataRow row in rows)
        {
            string[] fields =
            [
                row.Sequence,
                ValueOrNotAvailable(row.Dcr, dcr), ValueOrNotAvailable(row.Ls, ls), ValueOrNotAvailable(row.Rs, rs), ValueOrNotAvailable(row.HighLs, highLs), ValueOrNotAvailable(row.HighQ, highQ),
                ResultOrNotAvailable(row.Dcr, dcr), ResultOrNotAvailable(row.Ls, ls), ResultOrNotAvailable(row.Rs, rs), ResultOrNotAvailable(row.HighLs, highLs), ResultOrNotAvailable(row.HighQ, highQ), row.CompletedTime
            ];
            text.AppendLine(string.Join(',', fields));
        }

        return text.ToString();
    }

    private static ProductionDataMeasurementDefinition FindDefinition(IReadOnlyList<ProductionDataMeasurementDefinition> definitions, string name)
        => definitions.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? new ProductionDataMeasurementDefinition(name, "N/A", "N/A", null, null, false);

    private static void AppendSummary(StringBuilder text, string name, ProductionDataMeasurementDefinition definition, IEnumerable<ProductionDataSignal> signals)
    {
        IReadOnlyList<double> values = signals
            .Where(static value => value.HasValue)
            .Select(static value => value.NumericValue!.Value)
            .ToArray();
        text.Append($"{name}仪表中心值:{DefinitionValue(definition, definition.CenterValue)},{name}良品上限:{DefinitionValue(definition, definition.UpperLimit)},{name}良品下限:{DefinitionValue(definition, definition.LowerLimit)},{name}仪表最大值:{(values.Count == 0 ? "N/A" : FormatNumber(values.Max()))},{name}仪表最小值:{(values.Count == 0 ? "N/A" : FormatNumber(values.Min()))}");
        text.AppendLine();
    }

    private static string Header(string name, ProductionDataMeasurementDefinition definition)
        => $"{name}({(definition.IsEnabled ? ValueOrNotAvailable(definition.Unit) : "N/A")})";

    private static string DefinitionValue(ProductionDataMeasurementDefinition definition, object? value)
        => definition.IsEnabled ? ValueOrNotAvailable(value) : "N/A";

    private static string ValueOrNotAvailable(ProductionDataSignal signal, ProductionDataMeasurementDefinition definition)
        => definition.IsEnabled && signal.HasValue ? signal.Value : "N/A";

    private static string ResultOrNotAvailable(ProductionDataSignal signal, ProductionDataMeasurementDefinition definition)
        => definition.IsEnabled && signal.HasValue ? signal.Result : "N/A";

    private static string ValueOrNotAvailable(object? value)
        => value switch
        {
            null => "N/A",
            string text when string.IsNullOrWhiteSpace(text) => "N/A",
            string text => text,
            double number => FormatNumber(number),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "N/A"
        };

    private static string FormatNumber(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static IReadOnlyList<ProductionDataRow> ParseProductionRows(string content, string machineId)
    {
        var rows = new List<ProductionDataRow>();
        foreach (string line in content.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] fields = SplitCsv(line);
            if (fields.Length < 4)
            {
                continue;
            }

            ProductionDataSignal dcr = CreateSignal(fields, 1, 2, 3);
            ProductionDataSignal ls = IsMachine4(machineId) ? CreateSignal(fields, 4, 5, 6) : ProductionDataSignal.NotAvailable;
            ProductionDataSignal rs = IsMachine4(machineId) ? CreateSignal(fields, 7, 8, 9) : ProductionDataSignal.NotAvailable;
            string completedTime = GetCompletedTime(dcr, ls, rs);
            rows.Add(new ProductionDataRow(fields[0], dcr, ls, rs, ProductionDataSignal.NotAvailable, ProductionDataSignal.NotAvailable, completedTime));
        }

        return rows;
    }

    private static bool IsMachine4(string machineId) => string.Equals(machineId, "Machine_4_HAHH", StringComparison.OrdinalIgnoreCase);

    private static ProductionDataSignal CreateSignal(IReadOnlyList<string> fields, int valueIndex, int resultIndex, int timeIndex)
    {
        if (fields.Count <= timeIndex || IsNullValue(fields[valueIndex]))
        {
            return ProductionDataSignal.NotAvailable;
        }

        double? numericValue = double.TryParse(fields[valueIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : null;
        DateTime? time = DateTime.TryParse(fields[timeIndex], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed) ? parsed : null;
        return new ProductionDataSignal(fields[valueIndex], IsNullValue(fields[resultIndex]) ? "N/A" : fields[resultIndex], time, numericValue);
    }

    private static string GetCompletedTime(params ProductionDataSignal[] signals)
    {
        DateTime? completed = signals.Where(static signal => signal.Time.HasValue).Select(static signal => signal.Time).Max();
        return completed?.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? "N/A";
    }

    private static bool IsNullValue(string? value)
        => string.IsNullOrWhiteSpace(value) || string.Equals(value, "(NULL)", StringComparison.OrdinalIgnoreCase);

    private static string[] SplitCsv(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char current = line[index];
            if (current == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append(current);
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (current == ',' && !quoted)
            {
                values.Add(value.ToString().Trim());
                value.Clear();
            }
            else
            {
                value.Append(current);
            }
        }

        values.Add(value.ToString().Trim());
        return values.ToArray();
    }

    private sealed record ProductionDataRow(string Sequence, ProductionDataSignal Dcr, ProductionDataSignal Ls, ProductionDataSignal Rs, ProductionDataSignal HighLs, ProductionDataSignal HighQ, string CompletedTime);

    private sealed record ProductionDataSignal(string Value, string Result, DateTime? Time, double? NumericValue)
    {
        public static ProductionDataSignal NotAvailable { get; } = new("N/A", "N/A", null, null);
        public bool HasValue => NumericValue.HasValue;
    }

    private async Task AppendCsvRowAsync(string path, IReadOnlyList<string> headers, IReadOnlyList<string> values, CancellationToken cancellationToken)
    {
        await fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            bool needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            var text = new StringBuilder();
            if (needsHeader)
            {
                text.AppendLine(string.Join(',', headers.Select(Csv)));
            }

            text.AppendLine(string.Join(',', values.Select(Csv)));
            await File.AppendAllTextAsync(path, text.ToString(), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            fileGate.Release();
        }
    }

    private string BuildPath(string workOrderNo, DateTimeOffset time, string category)
    {
        string directory = string.IsNullOrWhiteSpace(optionsStore.Current.DirectoryPath) ? ProductionDataOptions.DefaultDirectoryPath : optionsStore.Current.DirectoryPath;
        return BuildPath(directory, workOrderNo, time, category);
    }

    private static string BuildSampleDataPath(string workOrderNo, DateTimeOffset time)
        => BuildPath(@"D:\SampleData", workOrderNo, time, "CompensateRecords", "yyyy-MM-dd");

    private string BuildProductionRecordPath(string workOrderNo, string operatorNo, DateTimeOffset time)
    {
        string directory = string.IsNullOrWhiteSpace(optionsStore.Current.DirectoryPath) ? ProductionDataOptions.DefaultDirectoryPath : optionsStore.Current.DirectoryPath;
        return Path.Combine(directory, $"{SanitizeFileNamePart(workOrderNo)}-{SanitizeFileNamePart(operatorNo)}-{time:yyyy-MM-dd-HH-mm-ss}.csv");
    }

    private static string BuildPath(string directory, string workOrderNo, DateTimeOffset time, string category, string dateFormat = "yyyyMMdd")
    {
        return Path.Combine(directory, $"{SanitizeFileNamePart(workOrderNo)}-{time.ToString(dateFormat, CultureInfo.InvariantCulture)}-{category}.csv");
    }

    private static string SanitizeFileNamePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Unknown" : new string(value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());

    private string T(string key, string fallback) => localizationService.T(key, fallback);
    private static string Csv(string value) => value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
