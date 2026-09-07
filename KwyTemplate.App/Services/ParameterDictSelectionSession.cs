namespace KwyTemplate.App.Services;

/// <summary>
/// 参数字典页面当前选中文件的应用内会话。
/// 仅用于二级导航切换后的界面恢复，不会加载、保存或修改工单内容。
/// </summary>
public sealed class ParameterDictSelectionSession
{
    public string? SelectedFileName { get; private set; }

    public void SetSelectedFileName(string? fileName)
        => SelectedFileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();
}
