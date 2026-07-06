namespace Kwy.Files;

/// <summary>
/// 图像路径辅助类，用于路径类型检测和图像格式过滤
/// </summary>
public static class ImagePathHelper
{
    /// <summary>
    /// 支持的图像格式扩展名
    /// </summary>
    private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(
        new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp" },
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 检查文件是否为支持的图像格式
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否为支持的图像格式</returns>
    public static bool IsSupportedImageFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var extension = Path.GetExtension(filePath);
        return SupportedImageExtensions.Contains(extension);
    }

    /// <summary>
    /// 获取文件夹中所有支持的图像文件
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="searchOption">搜索选项（是否包含子文件夹）</param>
    /// <returns>图像文件路径列表</returns>
    public static List<string> GetImageFilesFromFolder(string folderPath, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var imageFiles = new List<string>();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return imageFiles;

        try
        {
            var allFiles = Directory.GetFiles(folderPath, "*.*", searchOption);
            imageFiles = allFiles
                .Where(file => IsSupportedImageFile(file))
                .OrderBy(file => file)
                .ToList();
        }
        catch
        {
            // 忽略异常，返回空列表
        }

        return imageFiles;
    }

    /// <summary>
    /// 获取图像文件数量（用于统计）
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="searchOption">搜索选项</param>
    /// <returns>图像文件数量</returns>
    public static int GetImageFileCount(string folderPath, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        return GetImageFilesFromFolder(folderPath, searchOption).Count;
    }
}

