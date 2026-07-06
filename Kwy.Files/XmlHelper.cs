using System.Text;
using System.Xml.Serialization;

namespace Kwy.Files;

/// <summary>
/// XML 序列化/反序列化工具类（基于 System.Xml.Serialization）
/// </summary>
public static class XmlHelper
{
    // 默认 XML 序列化器缓存（提高性能）
    private static readonly Dictionary<Type, XmlSerializer> XmlSerializerCache = new();

    // 默认编码（与文件操作工具类保持一致）
    private static readonly Encoding DefaultEncoding = Encoding.UTF8;

    #region 同步方法：序列化并写入文件

    /// <summary>
    /// 将对象序列化为 XML 并写入文件（完整路径）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="filePath">完整文件路径</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="xmlSerializer">XML 序列化器（可选）</param>
    public static void Write<T>(string filePath, T obj, XmlSerializer? xmlSerializer = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(ErrorMessages.FilePathCannotBeEmpty, nameof(filePath));

        // 确保目录存在
        FileSystemHelper.CreateDirectoryIfNotExists(filePath);

        // 获取或创建 XML 序列化器
        var serializer = xmlSerializer ?? GetXmlSerializer<T>();

        // 序列化并写入文件
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, DefaultEncoding);
        serializer.Serialize(writer, obj);
    }

    /// <summary>
    /// 将对象序列化为 XML 并写入文件（目录 + 文件名）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="directory">目录路径</param>
    /// <param name="fileName">文件名（含 .xml 扩展名）</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="xmlSerializer">XML 序列化器（可选）</param>
    public static void Write<T>(string directory, string fileName, T obj, XmlSerializer? xmlSerializer = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        string filePath = Path.Combine(directory, fileName);
        Write(filePath, obj, xmlSerializer);
    }

    #endregion 同步方法：序列化并写入文件

    #region 同步方法：从文件反序列化

    /// <summary>
    /// 从 XML 文件读取并反序列化为对象（完整路径）
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">完整文件路径</param>
    /// <param name="xmlSerializer">XML 序列化器（可选）</param>
    /// <returns>反序列化后的对象</returns>
    public static T? Read<T>(string filePath, XmlSerializer? xmlSerializer = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(ErrorMessages.FilePathCannotBeEmpty, nameof(filePath));
        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException(ErrorMessages.FileNotFound, filePath);

        // 获取或创建 XML 序列化器
        var serializer = xmlSerializer ?? GetXmlSerializer<T>();

        // 读取文件并反序列化
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, DefaultEncoding);
        return (T?)serializer.Deserialize(reader);
    }

    /// <summary>
    /// 从 XML 文件读取并反序列化为对象（目录 + 文件名）
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="directory">目录路径</param>
    /// <param name="fileName">文件名（含 .xml 扩展名）</param>
    /// <param name="xmlSerializer">XML 序列化器（可选）</param>
    /// <returns>反序列化后的对象</returns>
    public static T? Read<T>(string directory, string fileName, XmlSerializer? xmlSerializer = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        string filePath = Path.Combine(directory, fileName);
        return Read<T>(filePath, xmlSerializer);
    }

    #endregion 同步方法：从文件反序列化

    #region 异步方法：序列化并写入文件

    /// <summary>
    /// 异步将对象序列化为 XML 并写入文件（完整路径）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="filePath">完整文件路径</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="xmlSerializer">XML 序列化器（可选）</param>
    public static async Task WriteAsync<T>(string filePath, T obj, XmlSerializer? xmlSerializer = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(ErrorMessages.FilePathCannotBeEmpty, nameof(filePath));

        // 确保目录存在
        FileSystemHelper.CreateDirectoryIfNotExists(filePath);

        // 获取或创建 XML 序列化器
        var serializer = xmlSerializer ?? GetXmlSerializer<T>();

        // 序列化并异步写入文件
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        using var writer = new StreamWriter(stream, DefaultEncoding);
        await Task.Run(() => serializer.Serialize(writer, obj)).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 异步将对象序列化为 XML 并写入文件（目录 + 文件名）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="directory">目录路径</param>
    /// <param name="fileName">文件名（含 .xml 扩展名）</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="xmlSerializer">XML 序列化器（可选）</param>
    public static async Task WriteAsync<T>(string directory, string fileName, T obj, XmlSerializer? xmlSerializer = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        string filePath = Path.Combine(directory, fileName);
        await WriteAsync(filePath, obj, xmlSerializer).ConfigureAwait(false);
    }

    #endregion 异步方法：序列化并写入文件

    #region 异步方法：从文件反序列化

    /// <summary>
    /// 异步从 XML 文件读取并反序列化为对象（完整路径）
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">完整文件路径</param>
    /// <param name="xmlSerializer">XML 序列化器（可选）</param>
    /// <returns>反序列化后的对象</returns>
    public static async Task<T?> ReadAsync<T>(string filePath, XmlSerializer? xmlSerializer = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(ErrorMessages.FilePathCannotBeEmpty, nameof(filePath));
        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException(ErrorMessages.FileNotFound, filePath);

        // 获取或创建 XML 序列化器
        var serializer = xmlSerializer ?? GetXmlSerializer<T>();

        // 异步读取文件并反序列化
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        using var reader = new StreamReader(stream, DefaultEncoding);
        return await Task.Run(() => (T?)serializer.Deserialize(reader)).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步从 XML 文件读取并反序列化为对象（目录 + 文件名）
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="directory">目录路径</param>
    /// <param name="fileName">文件名（含 .xml 扩展名）</param>
    /// <param name="xmlSerializer">XML 序列化器（可选）</param>
    /// <returns>反序列化后的对象</returns>
    public static async Task<T?> ReadAsync<T>(string directory, string fileName, XmlSerializer? xmlSerializer = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        string filePath = Path.Combine(directory, fileName);
        return await ReadAsync<T>(filePath, xmlSerializer).ConfigureAwait(false);
    }

    #endregion 异步方法：从文件反序列化

    #region 辅助方法

    /// <summary>
    /// 获取或创建 XML 序列化器（使用缓存提高性能）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <returns>XML 序列化器</returns>
    private static XmlSerializer GetXmlSerializer<T>()
    {
        var type = typeof(T);

        lock (XmlSerializerCache)
        {
            if (!XmlSerializerCache.TryGetValue(type, out var serializer))
            {
                serializer = new XmlSerializer(type);
                XmlSerializerCache[type] = serializer;
            }

            return serializer;
        }
    }

    #endregion 辅助方法
}