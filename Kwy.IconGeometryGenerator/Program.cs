using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace Kwy.IconGeometryGenerator;

internal static class Program
{
    private const double GlyphEmSize = 1000;

    private static int Main(string[] args)
    {
        if (!TryParseArguments(args, out GeneratorOptions? options, out string? error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 1;
        }

        try
        {
            int convertedCount = Convert(options!);
            Console.WriteLine($"已生成 {convertedCount} 个 Geometry 图标：{options!.OutputPath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"生成失败：{exception.Message}");
            return 1;
        }
    }

    private static int Convert(GeneratorOptions options)
    {
        string fontPath = Path.GetFullPath(options.FontPath);
        string sourcePath = Path.GetFullPath(options.SourcePath);
        string outputPath = Path.GetFullPath(options.OutputPath);

        if (!File.Exists(fontPath))
        {
            throw new FileNotFoundException("找不到字体文件。", fontPath);
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("找不到图标资源字典。", sourcePath);
        }

        if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("输出文件不能与源资源字典相同，请先生成到单独文件并检查结果。");
        }

        var glyphTypeface = new GlyphTypeface(new Uri(fontPath));
        XDocument document = XDocument.Load(sourcePath, LoadOptions.PreserveWhitespace);
        XElement root = document.Root ?? throw new InvalidOperationException("资源字典缺少根元素。");
        XNamespace presentation = root.Name.Namespace;
        var replacements = new List<(XElement Original, XElement Replacement)>();

        foreach (XElement element in root.Descendants().Where(static item => item.Name.LocalName == "String"))
        {
            string glyphText = element.Value;
            if (glyphText.Length != 1)
            {
                continue;
            }

            char character = glyphText[0];
            if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(character, out ushort glyphIndex) || glyphIndex == 0)
            {
                string key = GetResourceKey(element) ?? $"U+{(int)character:X4}";
                throw new InvalidOperationException($"字体不包含资源 '{key}' 使用的字符 U+{(int)character:X4}。");
            }

            Geometry outline = glyphTypeface.GetGlyphOutline(glyphIndex, GlyphEmSize, GlyphEmSize);
            Geometry normalizedOutline = Normalize(outline);
            string geometryText = GeometryToString(normalizedOutline);
            var replacement = new XElement(
                presentation + "Geometry",
                element.Attributes(),
                geometryText);
            replacements.Add((element, replacement));
        }

        foreach ((XElement original, XElement replacement) in replacements)
        {
            original.ReplaceWith(replacement);
        }

        XDocument outputDocument = options.BasePath is null
            ? document
            : MergeIntoBaseDictionary(options.BasePath, document);

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var writerSettings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "    ",
            NewLineChars = Environment.NewLine,
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = true
        };

        using XmlWriter writer = XmlWriter.Create(outputPath, writerSettings);
        outputDocument.Save(writer);
        return replacements.Count;
    }

    /// <summary>
    /// 以指定资源字典为基准，仅覆盖本次生成的同名资源键。
    /// </summary>
    private static XDocument MergeIntoBaseDictionary(string basePath, XDocument generatedDocument)
    {
        string fullBasePath = Path.GetFullPath(basePath);
        if (!File.Exists(fullBasePath))
        {
            throw new FileNotFoundException("找不到用于合并的基准资源字典。", fullBasePath);
        }

        XDocument baseDocument = XDocument.Load(fullBasePath, LoadOptions.PreserveWhitespace);
        XElement baseRoot = baseDocument.Root ?? throw new InvalidOperationException("基准资源字典缺少根元素。");
        XElement generatedRoot = generatedDocument.Root ?? throw new InvalidOperationException("生成资源字典缺少根元素。");
        var baseResources = baseRoot.Elements()
            .Select(item => (Element: item, Key: GetResourceKey(item)))
            .Where(static item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(static item => item.Key!, static item => item.Element, StringComparer.Ordinal);

        foreach (XElement generatedResource in generatedRoot.Elements())
        {
            string? key = GetResourceKey(generatedResource);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var replacement = new XElement(generatedResource);
            if (baseResources.TryGetValue(key, out XElement? existingResource))
            {
                existingResource.ReplaceWith(replacement);
                baseResources[key] = replacement;
            }
            else
            {
                baseRoot.Add(replacement);
                baseResources.Add(key, replacement);
            }
        }

        return baseDocument;
    }

    private static string GeometryToString(Geometry geometry)
    {
        var converter = new GeometryConverter();
        return converter.ConvertToInvariantString(geometry)
            ?? throw new InvalidOperationException("无法将字形轮廓转换为 Geometry 文本。");
    }

    /// <summary>
    /// 将字体字形移动到从 (0, 0) 开始的坐标区域。
    /// 字体轮廓通常以基线为原点，Y 坐标多为负数；若直接放进 Viewbox，
    /// 路径可能落在布局槽外而被裁剪。GetFlattenedPathGeometry 会将平移结果写入路径坐标。
    /// </summary>
    private static Geometry Normalize(Geometry outline)
    {
        Rect bounds = outline.Bounds;
        if (bounds.IsEmpty || (bounds.X == 0 && bounds.Y == 0))
        {
            return outline;
        }

        Geometry shiftedOutline = outline.CloneCurrentValue();
        shiftedOutline.Transform = new TranslateTransform(-bounds.X, -bounds.Y);
        return shiftedOutline.GetFlattenedPathGeometry();
    }

    private static string? GetResourceKey(XElement element)
        => element.Attributes().FirstOrDefault(static attribute => attribute.Name.LocalName == "Key")?.Value;

    private static bool TryParseArguments(string[] args, out GeneratorOptions? options, out string? error)
    {
        options = null;
        error = null;
        if ((args.Length != 6 && args.Length != 8)
            || !string.Equals(args[0], "--font", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(args[2], "--source", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(args[4], "--output", StringComparison.OrdinalIgnoreCase)
            || (args.Length == 8 && !string.Equals(args[6], "--base", StringComparison.OrdinalIgnoreCase)))
        {
            error = "参数格式不正确。";
            return false;
        }

        options = new GeneratorOptions(args[1], args[3], args[5], args.Length == 8 ? args[7] : null);
        return true;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("用法：");
        Console.WriteLine("  Kwy.IconGeometryGenerator --font <字体文件> --source <图标资源字典> --output <输出 XAML> [--base <基准资源字典>]");
        Console.WriteLine();
        Console.WriteLine("指定 --base 时，只替换基准资源字典中与本次生成结果同名的资源键，其余资源保持不变。\n");
    }

    private sealed record GeneratorOptions(string FontPath, string SourcePath, string OutputPath, string? BasePath);
}
