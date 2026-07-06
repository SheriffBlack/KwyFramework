using Kwy.MVVM.Core;

namespace KwyTemplate.App.Models;

public sealed class PlcRegisterPointModel : BindableBase
{
    private string address = string.Empty;
    private string name = string.Empty;
    private int value;
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

    public int Value
    {
        get => value;
        set => SetProperty(ref this.value, value);
    }

    public DateTime? LastUpdatedAt
    {
        get => lastUpdatedAt;
        set => SetProperty(ref lastUpdatedAt, value);
    }
}
