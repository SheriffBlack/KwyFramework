using System.Text;

namespace Kwy.Files;

/// <summary>
/// INI 文件读取、写入、解析和序列化工具类。
/// </summary>
public static class IniHelper
{
    public static Encoding DefaultEncoding { get; set; } = new UTF8Encoding(false);

    public static IniDocument Read(string filePath, Encoding? encoding = null)
    {
        ValidateFilePath(filePath);
        if (!System.IO.File.Exists(filePath))
        {
            throw new FileNotFoundException(ErrorMessages.FileNotFound, filePath);
        }

        return Parse(System.IO.File.ReadAllText(filePath, encoding ?? DefaultEncoding));
    }

    public static IniDocument Read(string directory, string fileName, Encoding? encoding = null)
        => Read(BuildPath(directory, fileName), encoding);

    public static async Task<IniDocument> ReadAsync(string filePath, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        ValidateFilePath(filePath);
        if (!System.IO.File.Exists(filePath))
        {
            throw new FileNotFoundException(ErrorMessages.FileNotFound, filePath);
        }

        string content = await System.IO.File.ReadAllTextAsync(filePath, encoding ?? DefaultEncoding, cancellationToken).ConfigureAwait(false);
        return Parse(content);
    }

    public static Task<IniDocument> ReadAsync(
        string directory,
        string fileName,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
        => ReadAsync(BuildPath(directory, fileName), encoding, cancellationToken);

    public static void Write(string filePath, IniDocument document, Encoding? encoding = null, bool atomic = true)
    {
        ValidateFilePath(filePath);
        ArgumentNullException.ThrowIfNull(document);
        FileSystemHelper.CreateDirectoryIfNotExists(filePath);
        WriteCore(filePath, Serialize(document), encoding ?? DefaultEncoding, atomic);
    }

    public static void Write(
        string directory,
        string fileName,
        IniDocument document,
        Encoding? encoding = null,
        bool atomic = true)
        => Write(BuildPath(directory, fileName), document, encoding, atomic);

    public static async Task WriteAsync(
        string filePath,
        IniDocument document,
        Encoding? encoding = null,
        bool atomic = true,
        CancellationToken cancellationToken = default)
    {
        ValidateFilePath(filePath);
        ArgumentNullException.ThrowIfNull(document);
        FileSystemHelper.CreateDirectoryIfNotExists(filePath);

        string content = Serialize(document);
        Encoding actualEncoding = encoding ?? DefaultEncoding;
        if (!atomic)
        {
            await System.IO.File.WriteAllTextAsync(filePath, content, actualEncoding, cancellationToken).ConfigureAwait(false);
            return;
        }

        string temporaryPath = CreateTemporaryPath(filePath);
        try
        {
            await System.IO.File.WriteAllTextAsync(temporaryPath, content, actualEncoding, cancellationToken).ConfigureAwait(false);
            System.IO.File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            if (System.IO.File.Exists(temporaryPath))
            {
                System.IO.File.Delete(temporaryPath);
            }
        }
    }

    public static Task WriteAsync(
        string directory,
        string fileName,
        IniDocument document,
        Encoding? encoding = null,
        bool atomic = true,
        CancellationToken cancellationToken = default)
        => WriteAsync(BuildPath(directory, fileName), document, encoding, atomic, cancellationToken);

    public static IniDocument Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var document = new IniDocument
        {
            NewLine = DetectNewLine(content),
            EndsWithNewLine = EndsWithNewLine(content)
        };

        string[] lines = SplitLines(content);
        IniSection? currentSection = null;
        for (int index = 0; index < lines.Length; index++)
        {
            string raw = lines[index];
            string trimmed = raw.Trim();
            int lineNumber = index + 1;

            if (trimmed.Length == 0)
            {
                document.Lines.Add(IniLine.Blank(raw));
                continue;
            }

            if (trimmed[0] is ';' or '#')
            {
                document.Lines.Add(IniLine.Comment(raw));
                continue;
            }

            if (trimmed[0] == '[' && trimmed[^1] == ']')
            {
                string name = IniDocument.ValidateName(trimmed[1..^1], nameof(content));
                var header = IniLine.Section(raw, name);
                document.AddParsedSection(header, name, lineNumber);
                currentSection = document.GetSection(name);
                continue;
            }

            int separatorIndex = FindSeparator(raw);
            if (separatorIndex < 0)
            {
                throw new FormatException($"Invalid INI line {lineNumber}: {raw}");
            }

            if (currentSection is null)
            {
                throw new FormatException($"INI key-value pair appears before the first section at line {lineNumber}.");
            }

            string key = IniDocument.ValidateName(raw[..separatorIndex], nameof(content));
            string valuePart = raw[(separatorIndex + 1)..];
            int valueStart = valuePart.Length - valuePart.TrimStart().Length;
            string valueAndSuffix = valuePart[valueStart..];
            SplitValueAndSuffix(valueAndSuffix, out string value, out string suffix);
            string prefix = raw[..(separatorIndex + 1)] + valuePart[..valueStart];
            var line = IniLine.KeyValue(raw, key, value, prefix, suffix);
            document.Lines.Add(line);
            currentSection.AddParsedValue(line, lineNumber);
        }

        return document;
    }

    public static string Serialize(IniDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        string content = string.Join(document.NewLine, document.Lines.Select(line => line.Raw));
        return document.EndsWithNewLine && document.Lines.Count > 0
            ? content + document.NewLine
            : content;
    }

    private static void WriteCore(string filePath, string content, Encoding encoding, bool atomic)
    {
        if (!atomic)
        {
            System.IO.File.WriteAllText(filePath, content, encoding);
            return;
        }

        string temporaryPath = CreateTemporaryPath(filePath);
        try
        {
            System.IO.File.WriteAllText(temporaryPath, content, encoding);
            System.IO.File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            if (System.IO.File.Exists(temporaryPath))
            {
                System.IO.File.Delete(temporaryPath);
            }
        }
    }

    private static string BuildPath(string directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));
        }

        return Path.Combine(directory, fileName);
    }

    private static void ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(ErrorMessages.FilePathCannotBeEmpty, nameof(filePath));
        }
    }

    private static int FindSeparator(string line)
    {
        int equals = line.IndexOf('=');
        int colon = line.IndexOf(':');
        return equals < 0 ? colon : colon < 0 ? equals : Math.Min(equals, colon);
    }

    private static void SplitValueAndSuffix(string valueAndSuffix, out string value, out string suffix)
    {
        for (int index = 0; index < valueAndSuffix.Length; index++)
        {
            if (valueAndSuffix[index] is not (';' or '#') || index == 0 || !char.IsWhiteSpace(valueAndSuffix[index - 1]))
            {
                continue;
            }

            int suffixStart = index - 1;
            while (suffixStart > 0 && char.IsWhiteSpace(valueAndSuffix[suffixStart - 1]))
            {
                suffixStart--;
            }

            value = valueAndSuffix[..suffixStart].TrimEnd();
            suffix = valueAndSuffix[suffixStart..];
            return;
        }

        value = valueAndSuffix.TrimEnd();
        suffix = valueAndSuffix[value.Length..];
    }

    private static string[] SplitLines(string content)
    {
        if (content.Length == 0)
        {
            return Array.Empty<string>();
        }

        string normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        return EndsWithNewLine(content) ? lines[..^1] : lines;
    }

    private static string DetectNewLine(string content)
        => content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n"
            : content.Contains('\n') ? "\n"
            : content.Contains('\r') ? "\r"
            : Environment.NewLine;

    private static bool EndsWithNewLine(string content)
        => content.EndsWith("\r\n", StringComparison.Ordinal)
            || content.EndsWith('\n')
            || content.EndsWith('\r');

    private static string CreateTemporaryPath(string filePath)
        => filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
}
