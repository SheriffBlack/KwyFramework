using Kwy.MVVM.Core;

namespace KwyTemplate.App.Models;

public sealed class PlcBoolPointModel : BindableBase
{
    private string address = string.Empty;
    private string communicationAddress = string.Empty;
    private string name = string.Empty;
    private bool value;
    private bool isMaster;
    private DateTime? lastUpdatedAt;

    public string Address
    {
        get => address;
        set => SetProperty(ref address, value);
    }

    public string CommunicationAddress
    {
        get => communicationAddress;
        set => SetProperty(ref communicationAddress, value);
    }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public bool Value
    {
        get
        {
            return value;
        }

        set
        {
            if (SetProperty(ref this.value, value))
            {
                RaisePropertyChanged(nameof(StatusText));
            }
        }
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

    public string StatusText
    {
        get
        {
            if (IsMaster)
            {
                return Value ? "全部锁定" : "全部释放";
            }

            return Value ? "已锁定" : "已释放";
        }
    }
}
