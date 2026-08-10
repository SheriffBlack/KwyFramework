using Kwy.MVVM.Core;

namespace KwyTemplate.App.Models;

public sealed class CheckFlowItemModel : BindableBase
{
    private bool isCompleted;
    private string displayName = string.Empty;
    private CheckFlowResultStatus resultStatus = CheckFlowResultStatus.Pending;

    public CheckFlowItemModel(string code, string displayName)
    {
        Code = code;
        this.displayName = displayName;
    }

    public string Code { get; }

    public string DisplayName
    {
        get => displayName;
        set => SetProperty(ref displayName, value ?? string.Empty);
    }

    public bool IsCompleted
    {
        get => isCompleted;
        set => SetProperty(ref isCompleted, value);
    }

    public CheckFlowResultStatus ResultStatus
    {
        get => resultStatus;
        private set
        {
            if (SetProperty(ref resultStatus, value))
            {
                RaisePropertyChanged(nameof(ResultText));
                RaisePropertyChanged(nameof(IsResultPassed));
            }
        }
    }

    public string ResultText => ResultStatus switch
    {
        CheckFlowResultStatus.Passed => "OK",
        CheckFlowResultStatus.Failed => "NG",
        _ => "--"
    };

    public bool IsResultPassed => ResultStatus == CheckFlowResultStatus.Passed;

    public void ResetResult() => ResultStatus = CheckFlowResultStatus.Pending;

    public void SetResult(bool isPassed)
        => ResultStatus = isPassed ? CheckFlowResultStatus.Passed : CheckFlowResultStatus.Failed;
}

public enum CheckFlowResultStatus
{
    Pending,
    Passed,
    Failed
}

