using System.Globalization;
using System.Reflection;
using System.Text;

namespace KwyTemplate.MES.Cyntec;

internal sealed class CyntecMesApiLogger
{
    private static readonly IReadOnlyDictionary<string, string> ApiNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["mesAPIconnect"] = "mesConnect",
        ["mesAPIwoQuery"] = "mesQuery",
        ["mesAPIcheckIn"] = "mesCheckIn",
        ["mesAPIcheckOut"] = "mesCheckOut",
        ["mesAPISTDPartsQuery"] = "mesSTDPartsQuery",
        ["mesStdPartsCheckResultSave"] = "mesSTDPartsCheckResultSave",
        ["mesAPIReelQuery"] = "mesReelQuery"
    };

    private static readonly string[] ReturnVariableOrder = ["returncode", "returnmessage"];
    private readonly CyntecMesOptions options;
    private readonly object syncRoot = new();
    private DateTime lastCleanupDate = DateTime.MinValue;

    public CyntecMesApiLogger(CyntecMesOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public CyntecMesApiLogScope WriteStart(object api)
    {
        ArgumentNullException.ThrowIfNull(api);

        DateTime timestamp = DateTime.Now;
        string apiName = GetApiName(api);
        IReadOnlyList<MesApiVariable> variables = GetStartVariables(api);
        string content = BuildBlock("START", timestamp, apiName, variables);
        WriteBlock(content, appendBlankLine: false);
        return new CyntecMesApiLogScope(apiName, variables.Select(static variable => variable.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    public void WriteEnd(object api, CyntecMesApiLogScope? scope)
    {
        ArgumentNullException.ThrowIfNull(api);

        DateTime timestamp = DateTime.Now;
        string apiName = scope?.ApiName ?? GetApiName(api);
        IReadOnlyList<MesApiVariable> variables = GetEndVariables(api, scope?.InputVariableNames);
        WriteBlock(BuildBlock("END", timestamp, apiName, variables), appendBlankLine: true);
    }

    private void WriteBlock(string content, bool appendBlankLine)
    {
        if (string.IsNullOrWhiteSpace(options.LogDirectory) || string.IsNullOrEmpty(content))
        {
            return;
        }

        lock (syncRoot)
        {
            Directory.CreateDirectory(options.LogDirectory);
            CleanupExpiredLogs(DateTime.Today);

            string filePath = Path.Combine(options.LogDirectory, DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".txt");
            string suffix = appendBlankLine ? Environment.NewLine + Environment.NewLine : Environment.NewLine;
            File.AppendAllText(filePath, content + suffix, new UTF8Encoding(false));
        }
    }

    private static IReadOnlyList<MesApiVariable> GetStartVariables(object api)
        => ReadVariables(api)
            .Where(static variable => !IsInternalVariable(variable.Name) && !IsReturnVariable(variable.Name))
            .ToArray();

    private static IReadOnlyList<MesApiVariable> GetEndVariables(object api, IReadOnlySet<string>? inputVariableNames)
    {
        List<MesApiVariable> variables = [];
        IReadOnlyList<MesApiVariable> source = ReadVariables(api);

        foreach (string returnName in ReturnVariableOrder)
        {
            MesApiVariable? variable = source.FirstOrDefault(item => string.Equals(item.Name, returnName, StringComparison.OrdinalIgnoreCase));
            if (variable != null)
            {
                variables.Add(variable);
            }
        }

        foreach (MesApiVariable variable in source)
        {
            if (IsInternalVariable(variable.Name)
                || IsReturnVariable(variable.Name)
                || (inputVariableNames?.Contains(variable.Name) ?? false))
            {
                continue;
            }

            variables.Add(variable);
        }

        return variables;
    }

    private static IReadOnlyList<MesApiVariable> ReadVariables(object api)
    {
        var variables = new List<MesApiVariable>();
        Type type = api.GetType();

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            AddVariable(variables, property.Name, property.GetValue(api));
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            AddVariable(variables, field.Name, field.GetValue(api));
        }

        return variables;
    }

    private static void AddVariable(List<MesApiVariable> variables, string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name) || value == null)
        {
            return;
        }

        if (value is string text && string.IsNullOrEmpty(text))
        {
            return;
        }

        Type valueType = value.GetType();
        if (!IsSimpleValueType(valueType))
        {
            return;
        }

        if (variables.Any(variable => string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        variables.Add(new MesApiVariable(name, value));
    }

    private static string BuildBlock(string marker, DateTime timestamp, string apiName, IReadOnlyList<MesApiVariable> variables)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"{marker}={timestamp:yyyyMMddHHmmss.fff}, MESAPI={FormatString(apiName)}");

        if (variables.Count == 0)
        {
            return builder.ToString();
        }

        builder.AppendLine();
        AppendVariables(builder, variables);
        return builder.ToString();
    }

    private static void AppendVariables(StringBuilder builder, IReadOnlyList<MesApiVariable> variables)
    {
        for (int i = 0; i < variables.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            MesApiVariable variable = variables[i];
            builder.Append(variable.Name);
            builder.Append('=');
            builder.Append(FormatValue(variable.Value));
        }
    }

    private static string FormatValue(object value)
        => value switch
        {
            string text => FormatString(text),
            char character => FormatString(character.ToString()),
            bool boolean => boolean ? "1" : "0",
            DateTime dateTime => FormatString(dateTime.ToString("yyyyMMddHHmmss.fff", CultureInfo.InvariantCulture)),
            DateTimeOffset dateTimeOffset => FormatString(dateTimeOffset.ToString("yyyyMMddHHmmss.fff", CultureInfo.InvariantCulture)),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => FormatString(value.ToString() ?? string.Empty)
        };

    private static string FormatString(string value)
        => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string GetApiName(object api)
    {
        string typeName = api.GetType().Name;
        if (ApiNameMap.TryGetValue(typeName, out string? apiName))
        {
            return apiName;
        }

        return typeName;
    }

    private static bool IsInternalVariable(string name)
        => string.Equals(name, "txnid", StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnVariable(string name)
        => ReturnVariableOrder.Any(returnName => string.Equals(returnName, name, StringComparison.OrdinalIgnoreCase));

    private static bool IsSimpleValueType(Type type)
    {
        Type targetType = Nullable.GetUnderlyingType(type) ?? type;
        return targetType.IsPrimitive
            || targetType.IsEnum
            || targetType == typeof(string)
            || targetType == typeof(decimal)
            || targetType == typeof(DateTime)
            || targetType == typeof(DateTimeOffset)
            || targetType == typeof(Guid);
    }

    private void CleanupExpiredLogs(DateTime today)
    {
        if (options.LogRetentionDays <= 0 || lastCleanupDate == today)
        {
            return;
        }

        lastCleanupDate = today;
        DateTime expireBefore = today.AddDays(-options.LogRetentionDays);
        foreach (string file in Directory.EnumerateFiles(options.LogDirectory, "*.txt"))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (DateTime.TryParseExact(fileName, "yyyyMMdd", null, DateTimeStyles.None, out DateTime logDate)
                && logDate < expireBefore)
            {
                TryDelete(file);
            }
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch
        {
            // MES log cleanup must never interrupt MES communication.
        }
    }

    private sealed record MesApiVariable(string Name, object Value);
}

internal sealed record CyntecMesApiLogScope(string ApiName, IReadOnlySet<string> InputVariableNames);

