using System.ComponentModel;
using Kwy.ComponentModel;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.App.Models;

/// <summary>
/// 编带参数 UI 元数据模型。MES 工单导入成功后会写入该模型；MES 离线时可在 SetView 手动应用并写入 PLC。
/// </summary>
public sealed class BraidOptions
{
    [DisplayName("前空格")]
    [DisplayNameKey("Braid.BeforeSpaceQty")]
    [InputType(InputType.NumberBox)]
    public int BeforeSpaceQty { get; set; }

    [DisplayName("包装数")]
    [DisplayNameKey("Braid.PackageQty")]
    [InputType(InputType.NumberBox)]
    public int PackageQty { get; set; }

    [DisplayName("后空格")]
    [DisplayNameKey("Braid.AfterSpaceQty")]
    [InputType(InputType.NumberBox)]
    public int AfterSpaceQty { get; set; }

    [DisplayName("样品数")]
    [DisplayNameKey("Braid.SampleQty")]
    [InputType(InputType.NumberBox)]
    public int SampleQty { get; set; }

    [DisplayName("空格二")]
    [DisplayNameKey("Braid.BlankQty")]
    [InputType(InputType.NumberBox)]
    public int BlankQty { get; set; }

    [DisplayName("后不封膜")]
    [DisplayNameKey("Braid.BackNoFilmQty")]
    [InputType(InputType.NumberBox)]
    public int BackNoFilmQty { get; set; }

    public static BraidOptions FromTapeSetup(MesWorkOrderTapeSetup? setup)
        => setup == null
            ? new BraidOptions()
            : new BraidOptions
            {
                BeforeSpaceQty = setup.BeforeSpaceQty ?? 0,
                PackageQty = setup.PackageQty ?? 0,
                AfterSpaceQty = setup.AfterSpaceQty ?? 0,
                SampleQty = setup.SampleQty ?? 0,
                BlankQty = setup.BlankQty ?? 0,
                BackNoFilmQty = setup.BackNoFilmQty ?? 0
            };

    public MesWorkOrderTapeSetup ToTapeSetup()
        => new(
            BeforeSpaceQty,
            PackageQty,
            AfterSpaceQty,
            SampleQty,
            BlankQty,
            BackNoFilmQty);
}
