using Kwy.MVVM.Core;

namespace KwyTemplate.App.Models;

public sealed class IoPointModel : BindableBase
{
    private string name = string.Empty;
    private int bitIndex;
    private bool isActive;
    private int triggerCount;
    private DateTime? lastUpdatedAt;

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public int BitIndex
    {
        get => bitIndex;
        set => SetProperty(ref bitIndex, value);
    }

    public bool IsActive
    {
        get => isActive;
        set => SetProperty(ref isActive, value);
    }

    public int TriggerCount
    {
        get => triggerCount;
        set => SetProperty(ref triggerCount, value);
    }

    public DateTime? LastUpdatedAt
    {
        get => lastUpdatedAt;
        set => SetProperty(ref lastUpdatedAt, value);
    }
}
