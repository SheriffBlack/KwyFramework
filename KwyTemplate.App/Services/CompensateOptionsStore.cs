using Kwy.Files;
using KwyTemplate.App.Models;
using System.IO;

namespace KwyTemplate.App.Services;

/// <summary>
/// 自动点检配置持久化入口。设置页编辑同一份配置，后续点检调度逻辑也从这里读取。
/// </summary>
public sealed class CompensateOptionsStore
{
    private static readonly string OptionsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "Config",
        "Compensate",
        "CompensateOptions.json");

    public string FilePath => OptionsFilePath;

    public CompensateOptions Current { get; private set; } = new();

    public CompensateOptionsLoadResult LoadOrCreate()
    {
        if (!File.Exists(OptionsFilePath))
        {
            Current = new CompensateOptions();
            JsonHelper.Write(OptionsFilePath, Current);
            return new CompensateOptionsLoadResult(Current, OptionsFilePath, true);
        }

        Current = JsonHelper.Read<CompensateOptions>(OptionsFilePath) ?? new CompensateOptions();
        return new CompensateOptionsLoadResult(Current, OptionsFilePath, false);
    }

    public async Task SaveAsync(CompensateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        await JsonHelper.WriteAsync(OptionsFilePath, options).ConfigureAwait(false);
        Current = options;
    }
}

public sealed record CompensateOptionsLoadResult(
    CompensateOptions Options,
    string FilePath,
    bool Created);