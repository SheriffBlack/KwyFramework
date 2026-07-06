namespace Kwy.ComponentModel;

/// <summary>
/// 显式指示 UI 引擎使用的输入控件类型
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class InputTypeAttribute : Attribute
{
    public InputType Type { get; }
    public InputTypeAttribute(InputType type) => Type = type;
}

