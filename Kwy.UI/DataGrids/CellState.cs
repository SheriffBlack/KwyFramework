using System.ComponentModel;

namespace Kwy.UI.DataGrids;

/// <summary>
/// 通用表格单元格状态。
/// </summary>
public class CellState : INotifyPropertyChanged
{
    private object? value;
    private bool? judge;
    private Action<Action>? propertyChangedDispatcher;

    /// <summary>
    /// Optional owner-provided dispatcher for property notifications.
    /// The data model remains UI-framework independent while a WPF owner can
    /// marshal notifications raised by a background production thread.
    /// </summary>
    public void SetPropertyChangedDispatcher(Action<Action>? dispatcher)
        => propertyChangedDispatcher = dispatcher;

    public object? Value
    {
        get => value;
        set
        {
            if (!Equals(this.value, value))
            {
                this.value = value;
                OnPropertyChanged(nameof(Value));
            }
        }
    }

    public bool? Judge
    {
        get => judge;
        set
        {
            if (judge != value)
            {
                judge = value;
                OnPropertyChanged(nameof(Judge));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChangedEventHandler? handler = PropertyChanged;
        if (handler == null)
        {
            return;
        }

        Action raise = () => handler(this, new PropertyChangedEventArgs(propertyName));
        Action<Action>? dispatcher = propertyChangedDispatcher;
        if (dispatcher == null)
        {
            raise();
            return;
        }

        dispatcher(raise);
    }
}
