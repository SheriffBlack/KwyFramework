using System.ComponentModel;
using System.Text.Json.Serialization;
using Kwy.ComponentModel;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Instruments.Dcr;

/// <summary>
/// ADEX AX1152D/AX1156 DCR meter configuration.
/// </summary>
public class AdexDcrConfig : IDeviceConfig
{
    [Browsable(false)]
    [JsonIgnore]
    public string SupportedModel => "ADEX_DCR";

    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("仪表型号")]
    [DisplayNameKey("Instrument.Dcr.Model")]
    [InputType(InputType.ComboBox)]
    [ItemsSource("AX1152D", "AX1156A")]
    public string Model { get; set; } = "AX1152D";

    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试模式")]
    [DisplayNameKey("Instrument.Dcr.TestMode")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("R", "D")]
    public string TestMode { get; set; } = "R";

    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试量程")]
    [DisplayNameKey("Instrument.Dcr.Range")]
    [InputType(InputType.ComboBox)]
    [ItemsSource("1mΩ", "10mΩ", "100mΩ", "1Ω", "10Ω", "100Ω", "1KΩ")]
    public string Range { get; set; } = "1Ω";

    [Category("基础设置")]
    [CategoryKey("Instrument.Category.Basic")]
    [DisplayName("测试速度")]
    [DisplayNameKey("Instrument.Dcr.Speed")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("FAST", "SLOW")]
    public string Speed { get; set; } = "SLOW";

    [Category("判定设置")]
    [CategoryKey("Instrument.Category.Judgment")]
    [DisplayName("上限值")]
    [DisplayNameKey("Instrument.Limit.UpperValue")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("Ω", "mΩ", "μΩ")]
    public double UpperLimitRaw { get; set; }

    [Browsable(false)]
    public string UpperLimitRawUnit { get; set; } = "mΩ";

    [Category("判定设置")]
    [CategoryKey("Instrument.Category.Judgment")]
    [DisplayName("下限值")]
    [DisplayNameKey("Instrument.Limit.LowerValue")]
    [InputType(InputType.TextBoxWithRadioButton)]
    [ItemsSource("Ω", "mΩ", "μΩ")]
    public double LowerLimitRaw { get; set; }

    [Browsable(false)]
    public string LowerLimitRawUnit { get; set; } = "mΩ";

    public bool Validate()
    {
        if (LowerLimitRaw < 0 || UpperLimitRaw < 0)
        {
            return false;
        }

        if (AdexDcr.ConvertLimitToOhms(UpperLimitRaw, UpperLimitRawUnit)
            < AdexDcr.ConvertLimitToOhms(LowerLimitRaw, LowerLimitRawUnit))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Model)
            || string.IsNullOrWhiteSpace(TestMode)
            || string.IsNullOrWhiteSpace(Range)
            || string.IsNullOrWhiteSpace(Speed))
        {
            return false;
        }

        _ = AdexDcr.MapModel(Model);
        _ = AdexDcr.MapRange(Range);
        _ = AdexDcr.MapSpeed(Speed);
        return true;
    }
}

