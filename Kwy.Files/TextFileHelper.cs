using System.Text;

namespace Kwy.Files;

/// <summary>
/// 文本文件操作工具类
/// 支持读取、写入、追加、清空等常用操作
/// </summary>
public static class TextFileHelper
{
    /// <summary>
    /// 文本文件默认编码（UTF-8 带 BOM）
    /// </summary>
    public static Encoding DefaultEncoding { get; set; } = Encoding.UTF8;

    #region 同步操作

    /// <summary>
    /// 读取文本文件内容
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <returns>文件内容字符串</returns>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    public static string Read(string filePath, Encoding? encoding = null)
    {
        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException(ErrorMessages.FileNotFound, filePath);

        Encoding actualEncoding = encoding ?? DefaultEncoding;
        return System.IO.File.ReadAllText(filePath, actualEncoding);
    }

    /// <summary>
    /// 读取指定目录下的文件内容（重载：支持目录 + 文件名）
    /// </summary>
    /// <param name="directory">文件所在目录</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <returns>文件内容</returns>
    public static string Read(string directory, string fileName, Encoding? encoding = null)
    {
        // 校验目录和文件名有效性
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(ErrorMessages.DirectoryNotFound, new Exception(directory));

        // 拼接完整路径（自动处理目录末尾的斜杠问题）
        string filePath = Path.Combine(directory, fileName);

        // 复用现有 Read 方法的逻辑（避免重复代码）
        return Read(filePath, encoding);
    }

    /// <summary>
    /// 写入内容到文本文件（覆盖原有内容）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">要写入的内容</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <param name="appendNewLine">是否自动添加换行符（默认true）</param>
    public static void Write(string filePath, string content, Encoding? encoding = null, bool appendNewLine = true)
    {
        // 确保目录存在
        FileSystemHelper.CreateDirectoryIfNotExists(filePath);

        Encoding actualEncoding = encoding ?? DefaultEncoding;
        string finalContent = appendNewLine ? content + Environment.NewLine : content;
        System.IO.File.WriteAllText(filePath, finalContent, actualEncoding);
    }

    /// <summary>
    /// 向指定目录下的文件写入内容（重载：支持目录 + 文件名）
    /// </summary>
    /// <param name="directory">文件所在目录</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    /// <param name="content">要写入的内容</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <param name="appendNewLine">是否自动添加换行符（默认true）</param>
    public static void Write(string directory, string fileName, string content, Encoding? encoding = null, bool appendNewLine = true)
    {
        // 校验目录和文件名
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        // 拼接完整路径（自动处理路径分隔符）
        string filePath = Path.Combine(directory, fileName);

        // 复用现有 Write 方法的核心逻辑
        Write(filePath, content, encoding, appendNewLine);
    }

    /// <summary>
    /// 追加内容到文本文件末尾
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">要追加的内容</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <param name="appendNewLine">是否自动添加换行符（默认true）</param>
    public static void Append(string filePath, string content, Encoding? encoding = null, bool appendNewLine = true)
    {
        FileSystemHelper.CreateDirectoryIfNotExists(filePath);

        Encoding actualEncoding = encoding ?? DefaultEncoding;
        string finalContent = appendNewLine ? content + Environment.NewLine : content;
        System.IO.File.AppendAllText(filePath, finalContent, actualEncoding);
    }

    /// <summary>
    /// 向指定目录下的文件追加内容（重载：支持目录 + 文件名）
    /// </summary>
    /// <param name="directory">文件所在目录</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    /// <param name="content">要追加的内容</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <param name="appendNewLine">是否自动添加换行符（默认true）</param>
    public static void Append(string directory, string fileName, string content, Encoding? encoding = null, bool appendNewLine = true)
    {
        // 校验目录和文件名
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        // 拼接完整路径
        string filePath = Path.Combine(directory, fileName);

        // 复用现有 Append 方法的逻辑
        Append(filePath, content, encoding, appendNewLine);
    }

    /// <summary>
    /// 清空文本文件内容（保留文件）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public static void Clear(string filePath)
    {
        if (System.IO.File.Exists(filePath))
            System.IO.File.WriteAllText(filePath, string.Empty, DefaultEncoding);
        else
            Write(filePath, string.Empty); // 若文件不存在，创建空文件
    }

    /// <summary>
    /// 清空指定目录下的文本文件内容（重载：支持目录 + 文件名）
    /// </summary>
    /// <param name="directory">文件所在目录</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    public static void Clear(string directory, string fileName)
    {
        // 校验目录和文件名
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        // 拼接完整路径
        string filePath = Path.Combine(directory, fileName);

        // 复用现有 Clear 方法的逻辑
        Clear(filePath);
    }

    /// <summary>
    /// 删除文本文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public static void Delete(string filePath)
    {
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);
    }

    /// <summary>
    /// 删除指定目录下的文本文件（重载：支持目录 + 文件名）
    /// </summary>
    /// <param name="directory">文件所在目录</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    public static void Delete(string directory, string fileName)
    {
        // 校验目录和文件名
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        // 拼接完整路径
        string filePath = Path.Combine(directory, fileName);

        // 复用现有 Delete 方法的逻辑
        Delete(filePath);
    }

    #endregion 同步操作

    #region 异步操作（适合大文件或UI线程中使用）

    /// <summary>
    /// 异步读取文本文件内容
    /// </summary>
    public static async Task<string> ReadAsync(string filePath, Encoding? encoding = null)
    {
        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException(ErrorMessages.FileNotFound, filePath);

        Encoding actualEncoding = encoding ?? DefaultEncoding;
        using (var stream = new StreamReader(filePath, actualEncoding))
        {
            return await stream.ReadToEndAsync();
        }
    }

    /// <summary>
    /// 异步读取指定目录下的文件内容（重载：支持目录 + 文件名）
    /// </summary>
    /// <param name="directory">文件所在目录</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <returns>文件内容（异步任务）</returns>
    public static async Task<string> ReadAsync(string directory, string fileName, Encoding? encoding = null)
    {
        // 校验目录和文件名（与同步方法保持一致）
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        // 拼接完整路径
        string filePath = Path.Combine(directory, fileName);

        // 复用异步核心逻辑（保持异步链条不中断）
        return await ReadAsync(filePath, encoding).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步写入内容到文本文件（覆盖原有内容）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">要写入的内容</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <param name="appendNewLine">是否自动添加换行符（默认true）</param>
    public static async Task WriteAsync(string filePath, string content, Encoding? encoding = null, bool appendNewLine = true)
    {
        FileSystemHelper.CreateDirectoryIfNotExists(filePath);

        Encoding actualEncoding = encoding ?? DefaultEncoding;
        string finalContent = appendNewLine ? content + Environment.NewLine : content;
        using (var stream = new StreamWriter(filePath, false, actualEncoding))
        {
            await stream.WriteAsync(finalContent);
        }
    }

    /// <summary>
    /// 异步写入内容到指定目录下的文本文件（重载：支持目录 + 文件名）
    /// </summary>
    /// <param name="directory">文件所在目录</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    /// <param name="content">要写入的内容</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <param name="appendNewLine">是否自动添加换行符（默认true）</param>
    /// <returns>异步任务</returns>
    public static async Task WriteAsync(string directory, string fileName, string content, Encoding? encoding = null, bool appendNewLine = true)
    {
        // 校验目录和文件名（与同步/其他异步方法保持一致）
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        // 拼接完整路径
        string filePath = Path.Combine(directory, fileName);

        // 复用异步写入逻辑（保持异步链条）
        await WriteAsync(filePath, content, encoding, appendNewLine).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步追加内容到文本文件末尾
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">要追加的内容</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <param name="appendNewLine">是否自动添加换行符（默认true）</param>
    public static async Task AppendAsync(string filePath, string content, Encoding? encoding = null, bool appendNewLine = true)
    {
        FileSystemHelper.CreateDirectoryIfNotExists(filePath);

        Encoding actualEncoding = encoding ?? DefaultEncoding;
        string finalContent = appendNewLine ? content + Environment.NewLine : content;
        using (var stream = new StreamWriter(filePath, true, actualEncoding))
        {
            await stream.WriteAsync(finalContent);
        }
    }

    /// <summary>
    /// 异步追加内容到指定目录下的文本文件末尾（重载：支持目录 + 文件名）
    /// </summary>
    /// <param name="directory">文件所在目录</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    /// <param name="content">要追加的内容</param>
    /// <param name="encoding">编码（默认使用 DefaultEncoding）</param>
    /// <param name="appendNewLine">是否自动添加换行符（默认true）</param>
    /// <returns>异步任务</returns>
    public static async Task AppendAsync(string directory, string fileName, string content, Encoding? encoding = null, bool appendNewLine = true)
    {
        // 校验目录和文件名（与其他方法保持一致）
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        // 拼接完整路径
        string filePath = Path.Combine(directory, fileName);

        // 复用异步追加逻辑（保持异步链条）
        await AppendAsync(filePath, content, encoding, appendNewLine).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步清空文本文件内容
    /// </summary>
    public static async Task ClearAsync(string filePath)
    {
        if (System.IO.File.Exists(filePath))
        {
            using (var stream = new StreamWriter(filePath, false, DefaultEncoding))
            {
                await stream.WriteAsync(string.Empty);
            }
        }
        else
        {
            await WriteAsync(filePath, string.Empty);
        }
    }

    /// <summary>
    /// 异步清空指定目录下的文本文件内容（重载：支持目录 + 文件名）
    /// </summary>
    /// <param name="directory">文件所在目录</param>
    /// <param name="fileName">文件名（含扩展名）</param>
    /// <returns>异步任务</returns>
    public static async Task ClearAsync(string directory, string fileName)
    {
        // 校验目录和文件名（与其他方法保持一致）
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(ErrorMessages.DirectoryPathCannotBeEmpty, nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(ErrorMessages.FileNameCannotBeEmpty, nameof(fileName));

        // 拼接完整路径
        string filePath = Path.Combine(directory, fileName);

        // 复用异步清空逻辑（保持异步链条）
        await ClearAsync(filePath).ConfigureAwait(false);
    }

    #endregion 异步操作（适合大文件或UI线程中使用）
}
