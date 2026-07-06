namespace Kwy.UI.Models;


public class NavigationItemModel
{
    public string DisplayText { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public bool IsVisibility { get; set; } = true;

    /// <summary>
    /// true → KwyRadioButton, false → RadioButton
    /// </summary>
    public bool HasIcon
    {
        get => Icon != null;
    }

    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 导航参数：用于区分同视图的不同实例（如 电阻1, 电阻2）
    /// </summary>
    public string Parameter { get; set; } = string.Empty;
}