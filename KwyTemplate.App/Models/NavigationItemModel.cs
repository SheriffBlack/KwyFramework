namespace KwyTemplate.App.Models;

public class NavigationItemModel
{
    public string DisplayText { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public bool IsVisibility { get; set; } = true;

    /// <summary>
    /// true → KwyRadioButton, false → RadioButton
    /// </summary>
    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 导航参数：用于区分同视图的不同实例（如 电阻1, 电阻2）
    /// </summary>
    public string Parameter { get; set; } = string.Empty;

    /// <summary>
    /// 导航权限码。为空表示不限制权限。
    /// </summary>
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>
    /// 导航选中状态使用的稳定键。同一个 ViewName 可以通过不同 Parameter 表示不同实例。
    /// </summary>
    public string NavigationKey => string.IsNullOrWhiteSpace(Parameter) ? ViewName : $"{ViewName}:{Parameter}";
}
