using Kwy.ComponentModel;
using Kwy.Device.Abstractions;
using System.ComponentModel;
using System.Text.Json.Serialization;


namespace Kwy.Device.Instruments.Adex;

public class AdexDcrConfig
{
    [JsonIgnore]
    [Browsable(false)]
    public string SupportedModel => "Adex_Dcr";

    [Category("基础设置")]
    [DisplayName("量程")]
    [InputType(InputType.RadioButton)]
    [ItemsSource("AUTO|100mΩ", "1Ω|10Ω|100Ω", "1KΩ|10KΩ|100KΩ", "1MΩ|10MΩ|100MΩ")]
    public string Range { get; set; } = "AUTO";

    public bool Validate() => true;
}
