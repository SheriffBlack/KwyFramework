using System.IO;
using Kwy.Files;
using KwyTemplate.App.Models;

namespace KwyTemplate.App.Services;

/// <summary>
/// 限值生成配置的持久化入口。
/// </summary>
public sealed class StandardLimitAutoGenerationOptionsStore
{
    private static readonly string OptionsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "Config",
        "StandardLimit",
        "StandardLimitAutoGenerationOptions.json");

    public StandardLimitAutoGenerationOptions Current { get; private set; } = new();

    public void LoadOrCreate()
    {
        if (!File.Exists(OptionsFilePath))
        {
            Current = new StandardLimitAutoGenerationOptions();
            JsonHelper.Write(OptionsFilePath, Current);
            return;
        }

        Current = JsonHelper.Read<StandardLimitAutoGenerationOptions>(OptionsFilePath) ?? new StandardLimitAutoGenerationOptions();
    }

    public async Task SaveAsync(StandardLimitAutoGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        await JsonHelper.WriteAsync(OptionsFilePath, options).ConfigureAwait(false);
        Current = options;
    }
}
