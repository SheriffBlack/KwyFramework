using System.IO;

namespace Kwy.Vision.WPF.Images;

public static class VisionMediaFileTypes
{
    public const string ImageFileDisplayName = "\u56fe\u50cf\u6587\u4ef6";
    public const string VideoFileDisplayName = "\u89c6\u9891\u6587\u4ef6";

    public static readonly IReadOnlySet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".jpg",
        ".jpeg",
        ".png",
        ".tif",
        ".tiff"
    };

    public static readonly IReadOnlySet<string> SupportedVideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".avi",
        ".mov",
        ".mkv",
        ".wmv"
    };

    public static IReadOnlySet<string> GetSupportedExtensions(VisionMediaKind kind)
        => kind == VisionMediaKind.Video ? SupportedVideoExtensions : SupportedImageExtensions;

    public static string CreateOpenFileFilter(VisionMediaKind kind)
        => CreateOpenFileFilter(
            kind == VisionMediaKind.Video ? VideoFileDisplayName : ImageFileDisplayName,
            GetSupportedExtensions(kind));

    public static string JoinSources(IEnumerable<string> sources)
        => string.Join(Path.PathSeparator, sources);

    public static IEnumerable<string> ExpandSources(string? value, VisionMediaKind kind)
        => ExpandSources(value, GetSupportedExtensions(kind));

    public static string? ResolveInitialDirectory(string? value, VisionMediaKind kind)
    {
        foreach (string source in SplitSources(value))
        {
            if (Directory.Exists(source))
            {
                return source;
            }

            if (File.Exists(source))
            {
                string? directory = Path.GetDirectoryName(source);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    return directory;
                }
            }
        }

        return GetDefaultMediaDirectory(kind);
    }

    public static string? ResolveInitialFileName(string? value)
    {
        string? firstFile = SplitSources(value).FirstOrDefault(File.Exists);
        return firstFile == null ? null : Path.GetFileName(firstFile);
    }

    public static IEnumerable<string> SplitSources(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<string> ExpandSources(string? value, IReadOnlySet<string> supportedExtensions)
    {
        foreach (string source in SplitSources(value))
        {
            if (Directory.Exists(source))
            {
                foreach (string file in Directory
                    .EnumerateFiles(source)
                    .Where(file => supportedExtensions.Contains(Path.GetExtension(file)))
                    .OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
            else if (File.Exists(source) && supportedExtensions.Contains(Path.GetExtension(source)))
            {
                yield return source;
            }
        }
    }

    private static string CreateOpenFileFilter(string displayName, IEnumerable<string> extensions)
    {
        string patterns = string.Join(";", extensions.Select(ToFileDialogPattern));
        return $"{displayName} ({patterns})|{patterns}|\u6240\u6709\u6587\u4ef6 (*.*)|*.*";
    }

    private static string ToFileDialogPattern(string extension)
        => extension.StartsWith(".", StringComparison.Ordinal) ? $"*{extension}" : $"*.{extension}";

    private static string? GetDefaultMediaDirectory(VisionMediaKind kind)
    {
        string path = Environment.GetFolderPath(kind == VisionMediaKind.Video
            ? Environment.SpecialFolder.MyVideos
            : Environment.SpecialFolder.MyPictures);

        return Directory.Exists(path) ? path : null;
    }
}
