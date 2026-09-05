using System.ComponentModel;
using Kwy.ComponentModel;

namespace KwyTemplate.App.Models;

/// <summary>
/// 根据标准件中心值一键生成判定上下限时使用的偏差规则。
/// 负偏差、正偏差均填写绝对值；实际下限由中心值减去负偏差得到，实际上限由中心值加上正偏差得到。
/// </summary>
public sealed class StandardLimitAutoGenerationOptions
{
    [Category("DCR 50mΩ及以下")]
    [CategoryKey("StandardLimitAutoGeneration.Category.DcrAtOrBelow50MilliOhm")]
    [DisplayName("− (mΩ)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? DcrAtOrBelow50MilliOhmNegativeDeviation { get; set; }

    [Category("DCR 50mΩ及以下")]
    [CategoryKey("StandardLimitAutoGeneration.Category.DcrAtOrBelow50MilliOhm")]
    [DisplayName("+ (mΩ)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? DcrAtOrBelow50MilliOhmPositiveDeviation { get; set; }

    [Category("DCR 大于50mΩ")]
    [CategoryKey("StandardLimitAutoGeneration.Category.DcrAbove50MilliOhm")]
    [DisplayName("− (%)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? DcrAbove50MilliOhmNegativeDeviationPercent { get; set; }

    [Category("DCR 大于50mΩ")]
    [CategoryKey("StandardLimitAutoGeneration.Category.DcrAbove50MilliOhm")]
    [DisplayName("+ (%)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? DcrAbove50MilliOhmPositiveDeviationPercent { get; set; }

    [Category("Ls")]
    [CategoryKey("StandardLimitAutoGeneration.Category.Ls")]
    [DisplayName("− (%)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? LsNegativeDeviationPercent { get; set; }

    [Category("Ls")]
    [CategoryKey("StandardLimitAutoGeneration.Category.Ls")]
    [DisplayName("+ (%)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? LsPositiveDeviationPercent { get; set; }

    [Category("Rs 0.5Ω及以下")]
    [CategoryKey("StandardLimitAutoGeneration.Category.RsAtOrBelow0Point5Ohm")]
    [DisplayName("− (mΩ)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? RsAtOrBelow0Point5OhmNegativeDeviation { get; set; }

    [Category("Rs 0.5Ω及以下")]
    [CategoryKey("StandardLimitAutoGeneration.Category.RsAtOrBelow0Point5Ohm")]
    [DisplayName("+ (mΩ)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? RsAtOrBelow0Point5OhmPositiveDeviation { get; set; }

    [Category("Rs 大于0.5Ω")]
    [CategoryKey("StandardLimitAutoGeneration.Category.RsAbove0Point5Ohm")]
    [DisplayName("− (%)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? RsAbove0Point5OhmNegativeDeviationPercent { get; set; }

    [Category("Rs 大于0.5Ω")]
    [CategoryKey("StandardLimitAutoGeneration.Category.RsAbove0Point5Ohm")]
    [DisplayName("+ (%)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? RsAbove0Point5OhmPositiveDeviationPercent { get; set; }

    [Category("Q")]
    [CategoryKey("StandardLimitAutoGeneration.Category.Q")]
    [DisplayName("下限")]
    [DisplayNameKey("StandardLimitAutoGeneration.LowerLimit")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? QLowerLimit { get; set; }

    [Category("Q")]
    [CategoryKey("StandardLimitAutoGeneration.Category.Q")]
    [DisplayName("上限")]
    [DisplayNameKey("StandardLimitAutoGeneration.UpperLimit")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? QUpperLimit { get; set; }

    [Category("高频Ls")]
    [CategoryKey("StandardLimitAutoGeneration.Category.HighLs")]
    [DisplayName("− (%)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? HighLsNegativeDeviationPercent { get; set; }

    [Category("高频Ls")]
    [CategoryKey("StandardLimitAutoGeneration.Category.HighLs")]
    [DisplayName("+ (%)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? HighLsPositiveDeviationPercent { get; set; }

    [Category("高频Rs")]
    [CategoryKey("StandardLimitAutoGeneration.Category.HighRs")]
    [DisplayName("− (%)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? HighRsNegativeDeviationPercent { get; set; }

    [Category("高频Rs")]
    [CategoryKey("StandardLimitAutoGeneration.Category.HighRs")]
    [DisplayName("+ (%)")]
    [InputType(InputType.TextBox), EditorWidth(100), GroupWidth(0.5)]
    public double? HighRsPositiveDeviationPercent { get; set; }
}
