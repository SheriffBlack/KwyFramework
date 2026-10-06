using Kwy.MVVM.Core;

namespace KwyPecvd.App.ViewModels;

/// <summary>
/// PECVD 首页的组合 ViewModel。
/// </summary>
public class HomeViewModel : BindableBase
{
    public PecvdTopologyViewModel Topology { get; } = new();
}
