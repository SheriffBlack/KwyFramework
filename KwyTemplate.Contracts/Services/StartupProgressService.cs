using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KwyTemplate.Contracts.Services;

public sealed class StartupProgressChangedEventArgs : EventArgs
{
    public StartupProgressChangedEventArgs(string currentItem, double progressValue, bool isCompleted)
    {
        CurrentItem = currentItem;
        ProgressValue = progressValue;
        IsCompleted = isCompleted;
    }

    public string CurrentItem { get; }

    public double ProgressValue { get; }

    public bool IsCompleted { get; }
}

public sealed class StartupProgressService : INotifyPropertyChanged
{
    private string currentItem = "准备启动...";
    private double progressValue;
    private bool isCompleted;

    public string CurrentItem
    {
        get => currentItem;
        private set
        {
            if (currentItem == value)
            {
                return;
            }

            currentItem = value;
            OnPropertyChanged();
        }
    }

    public double ProgressValue
    {
        get => progressValue;
        private set
        {
            double normalized = Math.Clamp(value, 0, 100);
            if (Math.Abs(progressValue - normalized) < 0.001)
            {
                return;
            }

            progressValue = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PercentText));
        }
    }

    public bool IsCompleted
    {
        get => isCompleted;
        private set
        {
            if (isCompleted == value)
            {
                return;
            }

            isCompleted = value;
            OnPropertyChanged();
        }
    }

    public string PercentText => $"[{ProgressValue:0}%]";

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<StartupProgressChangedEventArgs>? ProgressChanged;

    public void Report(string currentItem, double progressValue)
    {
        CurrentItem = string.IsNullOrWhiteSpace(currentItem) ? CurrentItem : currentItem;
        ProgressValue = progressValue;
        IsCompleted = false;
        RaiseProgressChanged();
    }

    public void Complete(string currentItem = "启动完成")
    {
        CurrentItem = currentItem;
        ProgressValue = 100;
        IsCompleted = true;
        RaiseProgressChanged();
    }

    private void RaiseProgressChanged()
        => ProgressChanged?.Invoke(this, new StartupProgressChangedEventArgs(CurrentItem, ProgressValue, IsCompleted));

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
