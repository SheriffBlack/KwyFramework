using Kwy.Device.Abstractions;
using KwyTemplate.App.Models;
using KwyTemplate.Device.Devices;
using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Services;

/// <summary>
/// 从当前 SetView 使用的运行时配置生成一份可编辑的本地机种配方。
/// 参数字典新建与已有配方保存共用 Mapper 的仪表快照格式，避免维护第二套 JSON 结构。
/// </summary>
public sealed class LocalWorkOrderRecipeRuntimeSnapshotFactory
{
    private readonly MachineBase machine;
    private readonly IMachineDeviceContext devices;
    private readonly BraidOptionsStore braidOptionsStore;
    private readonly MarkPrintOptionsStore markPrintOptionsStore;
    private readonly LocalWorkOrderRecipeMapper recipeMapper;

    public LocalWorkOrderRecipeRuntimeSnapshotFactory(
        MachineBase machine,
        IMachineDeviceContext devices,
        BraidOptionsStore braidOptionsStore,
        MarkPrintOptionsStore markPrintOptionsStore,
        LocalWorkOrderRecipeMapper recipeMapper)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.devices = devices ?? throw new ArgumentNullException(nameof(devices));
        this.braidOptionsStore = braidOptionsStore ?? throw new ArgumentNullException(nameof(braidOptionsStore));
        this.markPrintOptionsStore = markPrintOptionsStore ?? throw new ArgumentNullException(nameof(markPrintOptionsStore));
        this.recipeMapper = recipeMapper ?? throw new ArgumentNullException(nameof(recipeMapper));
    }

    public LocalWorkOrderRecipe Create(string machineType)
    {
        LocalWorkOrderRecipe recipe = recipeMapper.CreateEmpty(machineType, machine.GetType().Name);
        BraidOptions braid = braidOptionsStore.Current;
        recipe.TapeSetup = new LocalTapeSetup
        {
            BeforeSpaceQty = braid.BeforeSpaceQty,
            PackageQty = braid.PackageQty,
            AfterSpaceQty = braid.AfterSpaceQty,
            SampleQty = braid.SampleQty,
            BlankQty = braid.BlankQty
        };
        recipe.MaterialRequirements = new LocalMaterialRequirements();
        recipe.Parameters["MatGroupNo"] = string.Empty;
        recipe.Parameters["MarkPrintString"] = markPrintOptionsStore.Current.PrintString?.Trim() ?? string.Empty;
        recipeMapper.CaptureInstrumentConfigs(
            recipe,
            devices.Devices,
            machine.TestStations.SelectMany(static station => station.InstrumentDeviceIds));
        recipeMapper.RemoveRedundantRecipeData(recipe);
        return recipe;
    }
}
