using System.Collections.ObjectModel;
using System.Globalization;

namespace Kwy.Files;

/// <summary>
/// 表示一个 INI 文档，并保留原始 section、键、注释、空行和顺序。
/// </summary>
public sealed class IniDocument
{
    private readonly List<IniLine> lines = new();
    private readonly Dictionary<string, IniSection> sections = new(StringComparer.OrdinalIgnoreCase);

    internal IList<IniLine> Lines => lines;

    /// <summary>
    /// 获取文档中的 section，名称不区分大小写。
    /// </summary>
    public IReadOnlyCollection<IniSection> Sections
        => new ReadOnlyCollection<IniSection>(sections.Values.OrderBy(item => item.Order).ToList());

    /// <summary>
    /// 获取读取时识别到的换行符。
    /// </summary>
    public string NewLine { get; internal set; } = Environment.NewLine;

    /// <summary>
    /// 获取或设置文档末尾是否保留换行符。
    /// </summary>
    public bool EndsWithNewLine { get; set; }

    /// <summary>
    /// 按名称获取 section；不存在时抛出异常。
    /// </summary>
    public IniSection this[string sectionName] => GetSection(sectionName);

    public bool ContainsSection(string sectionName)
        => sections.ContainsKey(ValidateName(sectionName, nameof(sectionName)));

    public IniSection GetSection(string sectionName)
        => sections.TryGetValue(ValidateName(sectionName, nameof(sectionName)), out IniSection? section)
            ? section
            : throw new KeyNotFoundException($"INI section not found: {sectionName}");

    public bool TryGetSection(string sectionName, out IniSection section)
        => sections.TryGetValue(ValidateName(sectionName, nameof(sectionName)), out section!);

    public IniSection GetOrAddSection(string sectionName)
    {
        sectionName = ValidateName(sectionName, nameof(sectionName));
        if (sections.TryGetValue(sectionName, out IniSection? existing))
        {
            return existing;
        }

        if (lines.Count > 0 && lines[^1].Kind != IniLineKind.Blank)
        {
            lines.Add(IniLine.Blank(string.Empty));
        }

        var header = IniLine.Section($"[{sectionName}]", sectionName);
        lines.Add(header);
        var section = new IniSection(this, sectionName, sections.Count, header);
        sections.Add(sectionName, section);
        return section;
    }

    public bool RemoveSection(string sectionName)
    {
        sectionName = ValidateName(sectionName, nameof(sectionName));
        if (!sections.Remove(sectionName, out IniSection? section))
        {
            return false;
        }

        int start = lines.IndexOf(section.Header);
        int end = start + 1;
        while (end < lines.Count && lines[end].Kind != IniLineKind.Section)
        {
            end++;
        }

        lines.RemoveRange(start, end - start);
        return true;
    }

    internal void AddParsedSection(IniLine header, string name, int lineNumber)
    {
        if (sections.ContainsKey(name))
        {
            throw new FormatException($"Duplicate INI section '{name}' at line {lineNumber}.");
        }

        lines.Add(header);
        sections.Add(name, new IniSection(this, name, sections.Count, header));
    }

    internal static string ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("INI name cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}

/// <summary>
/// 表示 INI 文档中的一个 section。
/// </summary>
public sealed class IniSection
{
    private readonly IniDocument document;
    private readonly Dictionary<string, IniLine> values = new(StringComparer.OrdinalIgnoreCase);

    internal IniSection(IniDocument document, string name, int order, IniLine header)
    {
        this.document = document;
        Name = name;
        Order = order;
        Header = header;
    }

    public string Name { get; }

    public IReadOnlyCollection<string> Keys => values.Keys.ToArray();

    public string this[string key]
    {
        get => GetString(key);
        set => SetValue(key, value);
    }

    internal int Order { get; }

    internal IniLine Header { get; }

    public bool ContainsKey(string key)
        => values.ContainsKey(IniDocument.ValidateName(key, nameof(key)));

    public string GetString(string key)
        => TryGetString(key, out string? value)
            ? value
            : throw new KeyNotFoundException($"INI key not found: [{Name}] {key}");

    public string GetString(string key, string defaultValue)
        => TryGetString(key, out string? value) ? value : defaultValue;

    public bool TryGetString(string key, out string value)
    {
        key = IniDocument.ValidateName(key, nameof(key));
        if (values.TryGetValue(key, out IniLine? line))
        {
            value = line.Value!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public int GetInt32(string key)
    {
        string value = GetString(key).Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexadecimal))
        {
            return hexadecimal;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : throw CreateConversionException(key, value, "Int32");
    }

    public double GetDouble(string key)
    {
        string value = GetString(key).Trim();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : throw CreateConversionException(key, value, "Double");
    }

    public bool GetBoolean(string key)
    {
        string value = GetString(key).Trim();
        return value.ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => throw CreateConversionException(key, value, "Boolean")
        };
    }

    public void SetValue(string key, string value)
    {
        key = IniDocument.ValidateName(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value);

        if (values.TryGetValue(key, out IniLine? existing))
        {
            existing.SetValue(value);
            return;
        }

        var line = IniLine.KeyValue($"{key}={value}", key, value, $"{key}=", string.Empty);
        int headerIndex = document.Lines.IndexOf(Header);
        int insertionIndex = headerIndex + 1;
        while (insertionIndex < document.Lines.Count && document.Lines[insertionIndex].Kind != IniLineKind.Section)
        {
            insertionIndex++;
        }

        document.Lines.Insert(insertionIndex, line);
        values.Add(key, line);
    }

    public void SetValue(string key, int value)
        => SetValue(key, value.ToString(CultureInfo.InvariantCulture));

    public void SetValue(string key, double value)
        => SetValue(key, value.ToString("G17", CultureInfo.InvariantCulture));

    public void SetValue(string key, bool value)
        => SetValue(key, value ? "true" : "false");

    public bool Remove(string key)
    {
        key = IniDocument.ValidateName(key, nameof(key));
        if (!values.Remove(key, out IniLine? line))
        {
            return false;
        }

        document.Lines.Remove(line);
        return true;
    }

    internal void AddParsedValue(IniLine line, int lineNumber)
    {
        if (values.ContainsKey(line.Key!))
        {
            throw new FormatException($"Duplicate INI key '{line.Key}' in section '{Name}' at line {lineNumber}.");
        }

        values.Add(line.Key!, line);
    }

    private FormatException CreateConversionException(string key, string value, string typeName)
        => new($"INI value '[{Name}] {key}={value}' cannot be converted to {typeName}.");
}

internal enum IniLineKind
{
    Blank,
    Comment,
    Section,
    KeyValue
}

internal sealed class IniLine
{
    private IniLine(IniLineKind kind, string raw)
    {
        Kind = kind;
        Raw = raw;
    }

    public IniLineKind Kind { get; }
    public string Raw { get; private set; }
    public string? Key { get; private init; }
    public string? Value { get; private set; }
    public string Prefix { get; private init; } = string.Empty;
    public string Suffix { get; private init; } = string.Empty;

    public static IniLine Blank(string raw) => new(IniLineKind.Blank, raw);
    public static IniLine Comment(string raw) => new(IniLineKind.Comment, raw);
    public static IniLine Section(string raw, string name) => new(IniLineKind.Section, raw) { Key = name };

    public static IniLine KeyValue(string raw, string key, string value, string prefix, string suffix)
        => new(IniLineKind.KeyValue, raw) { Key = key, Value = value, Prefix = prefix, Suffix = suffix };

    public void SetValue(string value)
    {
        Value = value;
        Raw = Prefix + value + Suffix;
    }
}
