namespace Kwy.UI.WPF.Components.Logging;

/// <summary>
/// 日志列表项的最小显示契约。业务侧可直接实现该接口，也可以提供同名属性供绑定使用。
/// </summary>
public interface IKwyLogEntry
{
    string TimeText { get; }

    string Level { get; }

    string Message { get; }
}
