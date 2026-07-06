namespace Kwy.MVVM.Dialogs;

/// <summary>
/// 现代化的异步对话框服务接口。
/// 注入它，以便在业务逻辑中呼叫弹出页面。
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 以非模态的方式显示指定名称的对话框 (不阻塞当前操作流)。
    /// </summary>
    /// <param name="name">View 的全名或注册名</param>
    /// <param name="parameters">传入给弹窗的参数 (可选)</param>
    /// <param name="callback">关闭后的回调结果 (可选)</param>
    void Show(string name, IDialogParameters? parameters = null, Action<IDialogResult>? callback = null);

    /// <summary>
    /// 以模态的方式显示指定名称的对话框，并支持 await 异步等待结果。
    /// 彻底告别 Callback 嵌套！
    /// </summary>
    /// <param name="name">View 的全名或注册名</param>
    /// <param name="parameters">传入给弹窗的参数 (可选)</param>
    /// <returns>包含用户交互结果的 Task</returns>
    Task<IDialogResult> ShowDialogAsync(string name, IDialogParameters? parameters = null);
}