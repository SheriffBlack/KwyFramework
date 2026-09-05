namespace KwyTemplate.App.Models;

/// <summary>
/// 参数字典目录中的只读文件项。
/// </summary>
public sealed record ParameterDictFileItemModel(string FileName, DateTime ModifyTime, string FullPath);
