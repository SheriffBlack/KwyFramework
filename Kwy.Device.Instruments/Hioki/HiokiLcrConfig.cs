using Kwy.ComponentModel;
using Kwy.Device.Abstractions;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Kwy.Device.Instruments.Hioki;

/// <summary>
/// HIOKI LCR 测试仪通用配置模型 (全能版)
/// 支持 4 路参数同步配置与上下限设定
/// </summary>
public class HiokiLcrConfig : IDeviceConfig
{
    [JsonIgnore]
    [Browsable(false)]
    public string SupportedModel => "HIOKI_LCR";

    #region 参数 1
    [Category("参数1设置")]
    [DisplayName("测试项 1")]
    [Browsable(true)]
    [InputType(InputType.ComboBox)]
    [GroupWidth(0.5)]
    [ItemsSource(
        "Z", "Y", "PHAS", "R_S", "R_P",
        "C_S", "C_P", "D", "G", "X",
        "L_S", "L_P", "Q", "B", "RDC", "T",
        "OFF")]
    public string Parameter1 { get; set; } = "Z";

    [Category("参数1设置")]
    [DisplayName("上限 1")]
    [Browsable(true)]
    [InputType(InputType.TextBox)]
    public double Parameter1Max { get; set; } = 1000.0;

    [Category("参数1设置")]
    [DisplayName("下限 1")]
    [Browsable(true)]
    [InputType(InputType.TextBox)]
    public double Parameter1Min { get; set; } = 0.0;
    #endregion

    #region 参数 2
    [Category("参数2设置")]
    [DisplayName("测试项 2")]
    [Browsable(true)]
    [InputType(InputType.ComboBox)]
    [GroupWidth(0.5)]
    [ItemsSource(
        "Z", "Y", "PHAS", "R_S", "R_P",
        "C_S", "C_P", "D", "G", "X",
        "L_S", "L_P", "Q", "B", "RDC", "T",
        "OFF")]
    public string Parameter2 { get; set; } = "PHAS";

    [Category("参数2设置")]
    [DisplayName("上限 2")]
    [Browsable(true)]
    [InputType(InputType.TextBox)]
    public double Parameter2Max { get; set; } = 180.0;

    [Category("参数2设置")]
    [DisplayName("下限 2")]
    [Browsable(true)]
    [InputType(InputType.TextBox)]
    public double Parameter2Min { get; set; } = -180.0;
    #endregion

    #region 参数 3
    [Category("参数3设置")]
    [DisplayName("测试项 3")]
    [Browsable(true)]
    [InputType(InputType.ComboBox)]
    [GroupWidth(0.5)]
    [ItemsSource(
        "Z", "Y", "PHAS", "R_S", "R_P",
        "C_S", "C_P", "D", "G", "X",
        "L_S", "L_P", "Q", "B", "RDC", "T",
        "OFF")]
    public string Parameter3 { get; set; } = "OFF";

    [Category("参数3设置")]
    [DisplayName("上限 3")]
    [Browsable(true)]
    [InputType(InputType.TextBox)]
    public double Parameter3Max { get; set; } = 0.0;

    [Category("参数3设置")]
    [DisplayName("下限 3")]
    [Browsable(true)]
    [InputType(InputType.TextBox)]
    public double Parameter3Min { get; set; } = 0.0;
    #endregion

    #region 参数 4
    [Category("参数4设置")]
    [DisplayName("测试项 4")]
    [Browsable(true)]
    [InputType(InputType.ComboBox)]
    [GroupWidth(0.5)]
    [ItemsSource(
        "Z", "Y", "PHAS", "R_S", "R_P",
        "C_S", "C_P", "D", "G", "X",
        "L_S", "L_P", "Q", "B", "RDC", "T",
        "OFF")]
    //[ItemsSource(
    //    "OFF|T",
    //    "Z|Cs|Ls",
    //    "Y|Cp|Lp",
    //    "θ|D|Q",
    //    "Rs|G|B",
    //    "Rp|X|Rdc")]
    public string Parameter4 { get; set; } = "OFF";

    [Category("参数4设置")]
    [DisplayName("上限 4")]
    [Browsable(true)]
    [InputType(InputType.TextBox)]
    public double Parameter4Max { get; set; } = 0.0;

    [Category("参数4设置")]
    [DisplayName("下限 4")]
    [Browsable(true)]
    [InputType(InputType.TextBox)]
    public double Parameter4Min { get; set; } = 0.0;
    #endregion

    #region 通用测试条件

    [Category("基础设置")]
    [DisplayName("测试频率")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("Hz", "kHz", "MHz")]
    public double Frequency { get; set; } = 1000.0;

    [Browsable(false)]
    public string FrequencyUnit { get; set; } = "Hz";

    [Category("基础设置")]
    [DisplayName("测试电压")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("mV", "V")]
    public double Voltage { get; set; } = 1.0;

    [Browsable(false)]
    public string VoltageUnit { get; set; } = "mV";

    [Category("基础设置")]
    [DisplayName("测试延迟 (s)")]
    public double Delay { get; set; } = 0.0;

    [Category("基础设置")]
    [DisplayName("量程")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("AUTO|100mΩ", "1Ω|10Ω|100Ω", "1KΩ|10KΩ|100KΩ", "1MΩ|10MΩ|100MΩ")]
    public string Range { get; set; } = "AUTO";

    [Category("基础设置")]
    [DisplayName("测量速度")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("FAST", "MED", "SLOW", "SLOW2")]
    public string Speed { get; set; } = "MED";


    [Category("基础设置")]
    [DisplayName("触发模式")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("EXT", "INT")]
    public string TriggerMode { get; set; } = "EXT";

    [Category("基础设置")]
    [DisplayName("比较功能")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("ON", "OFF")]
    public string Comparator { get; set; } = "ON";

    #endregion

    public bool Validate() => true;
}