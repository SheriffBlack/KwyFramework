using Kwy.Files;
using KwyTemplate.App.Models;
using System.IO;

namespace KwyTemplate.App.Services;

public sealed class ProductionDataOptionsStore
{
    private static readonly string OptionsFilePath = Path.Combine(
        AppContext.BaseDirectory, "Config", "ProductionData", "ProductionDataOptions.json");

    public ProductionDataOptions Current { get; private set; } = new();

    public ProductionDataOptionsLoadResult LoadOrCreate()
    {
        Current = File.Exists(OptionsFilePath)
            ? JsonHelper.Read<ProductionDataOptions>(OptionsFilePath) ?? new ProductionDataOptions()
            : new ProductionDataOptions();
        bool requiresSave = !File.Exists(OptionsFilePath);
        if (string.Equals(Current.DirectoryPath, @"D:\ProductionData", StringComparison.OrdinalIgnoreCase))
        {
            Current.DirectoryPath = ProductionDataOptions.DefaultDirectoryPath;
            requiresSave = true;
        }

        if (requiresSave)
        {
            JsonHelper.Write(OptionsFilePath, Current);
        }

        return new ProductionDataOptionsLoadResult(Current, OptionsFilePath);
    }
}

public sealed record ProductionDataOptionsLoadResult(ProductionDataOptions Options, string FilePath);
