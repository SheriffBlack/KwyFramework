using System.ComponentModel;
using Kwy.ComponentModel;
using KwyTemplate.Contracts.Localization;

namespace KwyTemplate.App.Models;

/// <summary>
/// 程序设定模型。通过 UI 元数据驱动 KwyPropertyGrid 生成编辑界面。
/// </summary>
public sealed class ProgramSettingsModel
{
    [DisplayName("程序集标题")]
    [InputType(InputType.TextBox)]
    [EditorWidth(500)]
    [ReadOnly(true)]
    public string Title { get; set; } = "TP测包机";

    [DisplayName("产品名称")]
    [InputType(InputType.TextBox)]
    [EditorWidth(500)]
    [ReadOnly(true)]
    public string Product { get; set; } = "TP测包机";

    [DisplayName("程序集描述")]
    [InputType(InputType.TextBox)]
    [EditorWidth(500)]
    [ReadOnly(true)]
    public string Description { get; set; } = "电子元器件测试包装机";

    [DisplayName("公司名称")]
    [InputType(InputType.TextBox)]
    [EditorWidth(500)]
    [ReadOnly(true)]
    public string Company { get; set; } = "苏州和胜禹机电";

    [DisplayName("版权信息")]
    [InputType(InputType.TextBox)]
    [EditorWidth(500)]
    [ReadOnly(true)]
    public string Copyright { get; set; } = "Copyright © 2025-2026 苏州和胜禹机电 All Rights Reserved.";

    [DisplayName("程序集版本")]
    [InputType(InputType.TextBox)]
    [EditorWidth(500)]
    [ReadOnly(true)]
    public string Version { get; set; } = "1.0.0.0";

    [DisplayName("文件版本")]
    [InputType(InputType.TextBox)]
    [EditorWidth(500)]
    [ReadOnly(true)]
    public string FileVersion { get; set; } = "1.0.0.0";


    [DisplayName("界面语言")]
    [InputType(InputType.ComboBox)]
    public LanguageType Language { get; set; } = LanguageType.ZH_CN;
    [DisplayName("官网")]
    [InputType(InputType.TextBox)]
    [EditorWidth(500)]
    [ReadOnly(true)]
    public string OfficialUrl { get; set; } = "http://www.heshengyu.com";
}
