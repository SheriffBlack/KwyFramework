using System.IO;

namespace KwyTemplate.App.Models;

/// <summary>
/// 参数字典只读目录配置。
/// </summary>
public sealed class ParameterDictOptions
{
    /// <summary>
    /// 待读取目录；可在 Config/ParameterDict/ParameterDictOptions.json 中修改。
    /// </summary>
    public string DirectoryPath { get; set; } = Path.Combine(
        AppContext.BaseDirectory,
        "Config",
        "ParameterDict",
        "Files");

    /// <summary>
    /// 允许展示的文件扩展名。
    /// </summary>
    public List<string> AllowedExtensions { get; set; } = [".json", ".txt"];

    /// <summary>
    /// 新建参数字典时使用的扩展名，必须包含在 AllowedExtensions 中。
    /// </summary>
    public string DefaultExtension { get; set; } = ".json";
}
