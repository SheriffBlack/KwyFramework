using System.IO;
using System.Text;
using System.Collections.Concurrent;
using Kwy.Files;
using KwyTemplate.App.Models;

namespace KwyTemplate.App.Services;

/// <summary>本地机种配方 JSON 的读写入口。</summary>
public sealed class LocalWorkOrderRecipeStore
{
    private readonly ParameterDictOptionsStore optionsStore;
    private readonly ConcurrentDictionary<string, LocalWorkOrderRecipe> recipes = new(StringComparer.OrdinalIgnoreCase);

    public LocalWorkOrderRecipeStore(ParameterDictOptionsStore optionsStore)
        => this.optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));

    /// <summary>本地配方成功落盘后通知页面刷新文件元数据。</summary>
    public event EventHandler<LocalWorkOrderRecipeSavedEventArgs>? RecipeSaved;

    public string GetFilePath(string machineType)
    {
        string normalizedMachineType = NormalizeMachineType(machineType);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedMachineType);
        return Path.Combine(optionsStore.Current.DirectoryPath, $"{normalizedMachineType}.json");
    }

    public LocalWorkOrderRecipe? Load(string machineType)
    {
        string normalizedMachineType = NormalizeMachineType(machineType);
        if (string.IsNullOrWhiteSpace(normalizedMachineType))
        {
            return null;
        }

        if (recipes.TryGetValue(normalizedMachineType, out LocalWorkOrderRecipe? cached))
        {
            return cached;
        }

        return LoadFromFile(GetFilePath(normalizedMachineType));
    }

    public LocalWorkOrderRecipe? LoadFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            return null;
        }

        LocalWorkOrderRecipe? recipe = JsonHelper.Read<LocalWorkOrderRecipe>(filePath);
        if (recipe == null)
        {
            return null;
        }

        // 兼容按旧规则创建、但文件名已经是机种的 JSON：机种字段缺失时以文件名为准；
        // 同时清除扫码枪可能带入的 CR/LF、空格和零宽字符。
        string fileMachineType = NormalizeMachineType(Path.GetFileNameWithoutExtension(filePath));
        string storedMachineType = NormalizeMachineType(recipe.EquipmentType);
        recipe.EquipmentType = string.IsNullOrWhiteSpace(storedMachineType)
            ? fileMachineType
            : storedMachineType;
        recipes[recipe.EquipmentType] = recipe;
        return recipe;
    }

    public static string NormalizeMachineType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Normalize(NormalizationForm.FormKC);
        return new string(normalized
            .Where(static character => !char.IsWhiteSpace(character)
                && !char.IsControl(character)
                && character is not '\u200B' and not '\uFEFF')
            .ToArray());
    }

    public async Task SaveAsync(LocalWorkOrderRecipe recipe, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        string targetPath = string.IsNullOrWhiteSpace(filePath) ? GetFilePath(recipe.EquipmentType ?? string.Empty) : filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? optionsStore.Current.DirectoryPath);
        await JsonHelper.WriteAsync(targetPath, recipe).ConfigureAwait(false);
        string machineType = NormalizeMachineType(recipe.EquipmentType);
        if (!string.IsNullOrWhiteSpace(machineType))
        {
            recipes[machineType] = recipe;
        }
        RecipeSaved?.Invoke(this, new LocalWorkOrderRecipeSavedEventArgs(
            targetPath,
            File.GetLastWriteTime(targetPath)));
    }
}

public sealed record LocalWorkOrderRecipeSavedEventArgs(string FilePath, DateTime ModifyTime);
