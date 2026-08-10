using System.IO;
using Kwy.Files;
using KwyTemplate.App.Models;

namespace KwyTemplate.App.Services;

/// <summary>
/// 编带参数持久化入口。工单导入成功后刷新 Current；MES 离线时 SetView 可直接应用 Current 到机台 PLC。
/// </summary>
public sealed class BraidOptionsStore
{
    private static readonly string OptionsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "Config",
        "Braid",
        "BraidOptions.json");

    public string FilePath => OptionsFilePath;

    public BraidOptions Current { get; private set; } = new();

    public BraidOptionsLoadResult LoadOrCreate()
    {
        if (!File.Exists(OptionsFilePath))
        {
            Current = new BraidOptions();
            JsonHelper.Write(OptionsFilePath, Current);
            return new BraidOptionsLoadResult(Current, OptionsFilePath, true);
        }

        Current = JsonHelper.Read<BraidOptions>(OptionsFilePath) ?? new BraidOptions();
        return new BraidOptionsLoadResult(Current, OptionsFilePath, false);
    }

    public async Task SaveAsync(BraidOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        await JsonHelper.WriteAsync(OptionsFilePath, options).ConfigureAwait(false);
        Current = options;
    }
}

public sealed record BraidOptionsLoadResult(
    BraidOptions Options,
    string FilePath,
    bool Created);