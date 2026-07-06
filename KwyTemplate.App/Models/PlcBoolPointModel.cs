using Kwy.MVVM.Core;

namespace KwyTemplate.App.Models;

public sealed class PlcBoolPointModel : BindableBase
{
    private string address = string.Empty;
    private string name = string.Empty;
    private bool value;
    private bool isMaster;
    private DateTime? lastUpdatedAt;

    public string Address
    {
        get => address;
        set => SetProperty(ref address, value);
    }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public bool Value
    {
        get => value;
        set => SetProperty(ref this.value, value);
    }

    public bool IsMaster
    {
        get => isMaster;
        set => SetProperty(ref isMaster, value);
    }

    public DateTime? LastUpdatedAt
    {
        get => lastUpdatedAt;
        set => SetProperty(ref lastUpdatedAt, value);
    }
}
