namespace Kwy.UI.Threading;

/// <summary>
/// Schedules presentation updates without coupling presentation models to a UI framework.
/// </summary>
public interface IUiDispatcher
{
    bool CheckAccess();

    void Post(Action action);
}
