using System.IO;
using Kwy.Files;
using KwyTemplate.App.Models;

namespace KwyTemplate.App.Services;

/// <summary>
/// 参数字典目录配置的持久化入口。
/// </summary>
public sealed class ParameterDictOptionsStore
{
    private static readonly string OptionsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "Config",
        "ParameterDict",
        "ParameterDictOptions.json");

    public string FilePath => OptionsFilePath;

    public ParameterDictOptions Current { get; private set; } = new();

    public ParameterDictOptionsLoadResult LoadOrCreate()
    {
        if (!File.Exists(OptionsFilePath))
        {
            Current = new ParameterDictOptions();
            JsonHelper.Write(OptionsFilePath, Current);
            return new ParameterDictOptionsLoadResult(Current, OptionsFilePath, true);
        }

        Current = JsonHelper.Read<ParameterDictOptions>(OptionsFilePath) ?? new ParameterDictOptions();
        return new ParameterDictOptionsLoadResult(Current, OptionsFilePath, false);
    }
}

public sealed record ParameterDictOptionsLoadResult(
    ParameterDictOptions Options,
    string FilePath,
    bool Created);
