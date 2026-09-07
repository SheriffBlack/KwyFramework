using System.ComponentModel;

namespace KwyTemplate.Contracts.Services;

/// <summary>
/// 生产运行上下文，只暴露 Flow/MES 等底层业务需要读取的现场信息，避免直接依赖 HomeView 或 App ViewModel。
/// </summary>
public interface IProductionRuntimeContext : INotifyPropertyChanged
{
    string WorkOrderNo { get; set; }

    string OperatorNo { get; set; }

    bool IsResultGridDataEnabled { get; set; }
}