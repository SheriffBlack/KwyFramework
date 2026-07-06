namespace Kwy.Files;

/// <summary>
    /// 路径类型枚举
    /// </summary>
    public enum PathType
    {
        /// <summary>
        /// 无效路径
        /// </summary>
        Invalid,

        /// <summary>
        /// 文件夹
        /// </summary>
        Folder,

        /// <summary>
        /// 文件
        /// </summary>
        File
    }


/// <summary>
/// 真实读取硬盘的操作
/// 比如创建文件夹、获取文件列表、判断文件是否存在
/// </summary>
public static class FileSystemHelper
{
    /// <summary>
    /// 获取指定目录下匹配后缀的文件列表
    /// </summary>
    /// <param name="path">目录路径</param>
    /// <param name="suffix">文件后缀匹配模式（默认"*.json"）</param>
    /// <returns>文件路径数组，如果目录不存在或为空则返回空数组</returns>
    public static string[] GetFileCatalog(string path, string suffix = "*.json")
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.GetFiles(path, suffix);
        }
        catch
        {
            // 忽略异常，返回空数组
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 确保文件所在目录存在（不存在则创建）
    /// </summary>
    public static void CreateDirectoryIfNotExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// 检测路径类型（文件夹或文件）
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>路径类型</returns>
    public static PathType DetectPathType(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return PathType.Invalid;

        if (Directory.Exists(path))
            return PathType.Folder;

        if (File.Exists(path))
            return PathType.File;

        return PathType.Invalid;
    }
}
