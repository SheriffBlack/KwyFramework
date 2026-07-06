namespace Kwy.ComponentModel;


/// <summary>
/// 输入控件类型枚举（抽象协议层，由各平台 UI 库实现具体渲染）
/// </summary>
public enum InputType
{
    /// <summary>
    /// 文本框
    /// </summary>
    TextBox,

    /// <summary>
    /// 数值输入框
    /// </summary>
    NumberBox,

    /// <summary>
    /// 下拉框
    /// </summary>
    ComboBox,

    /// <summary>
    /// 日期选择器
    /// </summary>
    DatePicker,

    /// <summary>
    /// 切换按钮（开关）
    /// </summary>
    ToggleButton,

    /// <summary>
    /// 普通按钮
    /// </summary>
    Button,

    /// <summary>
    /// 文本块（只读显示）
    /// </summary>
    TextBlock,

    /// <summary>
    /// 列表框
    /// </summary>
    ListBox,

    /// <summary>
    /// 单选按钮组
    /// </summary>
    RadioButton,

    /// <summary>
    /// 文本框 + 单选组 (带单位选择)
    /// </summary>
    TextBoxWithRadioButton
}
