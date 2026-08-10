namespace KwyTemplate.Contracts.Services;

/// <summary>
/// 生产数据输出目录配置。
/// 具体客户 MES 模块可以实现该接口，Flow 层只按该抽象保存本地结果文件。
/// </summary>
public interface IProductionOutputOptions
{
    string OutputDirectory { get; }

    string SummaryDirectory { get; }
}
