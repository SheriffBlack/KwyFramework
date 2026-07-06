using Kwy.MVVM.Core;

namespace Kwy.MVVM.Regions;

/// <summary>
/// 导航参数接口。
/// </summary>
public interface INavigationParameters : IParameters
{
}

/// <summary>
/// 导航参数类。供 ViewModel 之间传递状态。
/// </summary>
public class NavigationParameters : ParametersBase, INavigationParameters
{
    public NavigationParameters() : base()
    {
    }

    public NavigationParameters(string query) : base(query)
    {
    }
}