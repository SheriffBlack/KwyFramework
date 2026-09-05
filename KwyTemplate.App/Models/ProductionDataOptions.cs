namespace KwyTemplate.App.Models;

/// <summary>MES contract files之外的本地生产数据归档位置。</summary>
public sealed class ProductionDataOptions
{
    public const string DefaultDirectoryPath = @"D:\MeasureData";

    public string DirectoryPath { get; set; } = DefaultDirectoryPath;
}
