namespace KwyTemplate.App.Models;

public sealed class StandardSampleState
{
    public StandardSamplePanelModel StandardSample { get; } = new("标准件");

    public StandardSamplePanelModel ConfirmSample { get; } = new("确认件");

    public void ClearAll()
    {
        StandardSample.ClearAll();
        ConfirmSample.ClearAll();
    }
}
