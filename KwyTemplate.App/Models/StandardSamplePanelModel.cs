using System.Collections.ObjectModel;
using Kwy.MVVM.Core;

namespace KwyTemplate.App.Models;

public sealed class StandardSamplePanelModel : BindableBase
{
    private const int SampleCodeLength = 7;

    private string sampleCode = string.Empty;
    private string issueDate = string.Empty;
    private string expireDate = string.Empty;
    private string lowerLimit = string.Empty;
    private string upperLimit = string.Empty;
    private string standardValue = string.Empty;
    private string unit = string.Empty;
    private string statusMessage = string.Empty;
    private bool isQuerying;

    public StandardSamplePanelModel(string title)
    {
        Title = title;
    }

    public string Title { get; }

    public ObservableCollection<StandardSampleLimitItemModel> LimitItems { get; } = [];

    public string SampleCode
    {
        get => sampleCode;
        set => SetProperty(ref sampleCode, CleanSampleCode(value));
    }

    private static string CleanSampleCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string noLeftBrackets = value.TrimStart('{');
        int splitIndex = noLeftBrackets.IndexOf('{');
        string cleaned = splitIndex > 0
            ? noLeftBrackets[..splitIndex]
            : noLeftBrackets;

        string sampleCode = cleaned.Trim();
        return sampleCode.Length > SampleCodeLength
            ? sampleCode[..SampleCodeLength]
            : sampleCode;
    }
    public string IssueDate
    {
        get => issueDate;
        set => SetProperty(ref issueDate, value ?? string.Empty);
    }

    public string ExpireDate
    {
        get => expireDate;
        set => SetProperty(ref expireDate, value ?? string.Empty);
    }

    public string LowerLimit
    {
        get => lowerLimit;
        set => SetProperty(ref lowerLimit, value ?? string.Empty);
    }

    public string UpperLimit
    {
        get => upperLimit;
        set => SetProperty(ref upperLimit, value ?? string.Empty);
    }

    public string StandardValue
    {
        get => standardValue;
        set => SetProperty(ref standardValue, value ?? string.Empty);
    }

    public string Unit
    {
        get => unit;
        set => SetProperty(ref unit, value ?? string.Empty);
    }

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value ?? string.Empty);
    }

    public bool IsQuerying
    {
        get => isQuerying;
        set => SetProperty(ref isQuerying, value);
    }
    public void ClearAll()
    {
        SampleCode = string.Empty;
        IsQuerying = false;
        ClearResult();
    }

    public void ClearResult()
    {
        IssueDate = string.Empty;
        ExpireDate = string.Empty;
        LowerLimit = string.Empty;
        UpperLimit = string.Empty;
        StandardValue = string.Empty;
        Unit = string.Empty;
        foreach (StandardSampleLimitItemModel item in LimitItems)
        {
            item.ClearValue();
        }

        StatusMessage = string.Empty;
    }
}
