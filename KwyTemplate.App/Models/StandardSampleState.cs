namespace KwyTemplate.App.Models;

public sealed class StandardSampleState
{
    public StandardSamplePanelModel StandardSample { get; } = new("标准件");

    public StandardSamplePanelModel ConfirmSample { get; } = new("确认件");

    /// <summary>
    /// 标准件与确认件作为一个点检上下文被整体清空后触发。
    /// 单独重新查询某一面板不会触发该事件。
    /// </summary>
    public event EventHandler? Cleared;

    public void ClearAll()
    {
        StandardSample.ClearAll();
        ConfirmSample.ClearAll();
        Cleared?.Invoke(this, EventArgs.Empty);
    }
}
