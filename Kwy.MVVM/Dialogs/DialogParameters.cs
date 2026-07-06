using Kwy.MVVM.Core;

namespace Kwy.MVVM.Dialogs;

/// <summary>
/// 对话框参数接口，用于在对话框传递状态。
/// </summary>
public interface IDialogParameters : IParameters
{
}

/// <summary>
/// 对话框参数类。兼容 Prism 语法。
/// </summary>
public class DialogParameters : ParametersBase, IDialogParameters
{
    public DialogParameters() : base()
    {
    }

    /// <summary>
    /// 通过查询字符串初始化参数 (如: "message=hello&icon=Warning")
    /// </summary>
    public DialogParameters(string query) : base(query) { }
}