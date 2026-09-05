using KwyTemplate.App.Models;
using System.IO;

namespace KwyTemplate.App.Services;

/// <summary>
/// 当前离线编辑的本地机种配方。
/// 它只保存运行内存状态，磁盘读写仍由 <see cref="LocalWorkOrderRecipeStore"/> 负责。
/// </summary>
public sealed class LocalWorkOrderRecipeSession
{
    public LocalWorkOrderRecipe? Current { get; private set; }

    /// <summary>当前编辑配方实际加载自的 JSON 文件；为空时按参数字典默认目录保存。</summary>
    public string? CurrentFilePath { get; private set; }

    public void SetCurrent(LocalWorkOrderRecipe recipe, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        bool isDifferentRecipe = Current == null
            || !string.Equals(Current.EquipmentType, recipe.EquipmentType, StringComparison.OrdinalIgnoreCase);
        Current = recipe;
        CurrentFilePath = !string.IsNullOrWhiteSpace(filePath)
            ? Path.GetFullPath(filePath)
            : isDifferentRecipe ? null : CurrentFilePath;
    }

    public LocalWorkOrderRecipe GetOrCreate(string machineType, string machineProfileKey, LocalWorkOrderRecipeMapper mapper)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineType);
        ArgumentNullException.ThrowIfNull(mapper);

        if (Current is null || !string.Equals(Current.EquipmentType, machineType, StringComparison.OrdinalIgnoreCase))
        {
            Current = mapper.CreateEmpty(machineType, machineProfileKey);
            CurrentFilePath = null;
        }

        return Current;
    }

    public void Clear()
    {
        Current = null;
        CurrentFilePath = null;
    }
}
