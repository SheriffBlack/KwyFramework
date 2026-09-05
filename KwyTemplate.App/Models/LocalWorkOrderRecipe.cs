using KwyTemplate.MES.Abstract.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KwyTemplate.App.Models;

/// <summary>
/// 本地保存的统一机种配方。
/// 该模型不包含客户 MES 的字段名；客户格式由各自 MES 适配器转换后再进入本模型。
/// </summary>
public sealed class LocalWorkOrderRecipe
{
    public int SchemaVersion { get; set; } = 2;

    /// <summary>创建此配方时的机型标识，仅用于追溯；不作为工单与机型的一对一绑定。</summary>
    public string? MachineProfileKey { get; set; }

    public string Source { get; set; } = "Local";

    public string? ProductNo { get; set; }

    public string? ProductName { get; set; }

    /// <summary>仅用于读取旧版 JSON；保存前会迁移到 Parameters["MatGroupNo"] 并清空。</summary>
    [JsonPropertyName("RecipeName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyRecipeName { get; set; }

    /// <summary>仅用于读取旧版 JSON；本地工单不再维护配方修订号。</summary>
    [JsonPropertyName("RecipeRevision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyRecipeRevision { get; set; }

    /// <summary>本地配方的主键，同时也是 JSON 文件名；例如以 X 结尾时触发现有特殊机种逻辑。</summary>
    public string? EquipmentType { get; set; }

    /// <summary>工单业务参数，不保存仪表参数。</summary>
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 工位仪表参数快照。键直接使用 DeviceCatalog 的设备标识，值与对应设备
    /// 参数 JSON 同构，避免上下限、频率、量程在多个字段重复保存。
    /// </summary>
    public Dictionary<string, JsonElement> InstrumentConfigs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>仅用于读取旧版配方；新版保存时不再写入。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LocalMeasurementSetup>? Measurements { get; set; }

    public LocalMaterialRequirements? MaterialRequirements { get; set; }

    public LocalTapeSetup? TapeSetup { get; set; }

    public int? StandardSampleCheckInterval { get; set; }
}

public sealed class LocalMeasurementSetup
{
    public string ParameterId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public double? LowerLimit { get; set; }

    public double? UpperLimit { get; set; }

    public string? Unit { get; set; }

    public string? Range { get; set; }
}

public sealed class LocalMaterialRequirements
{
    public string? TablePaperMatNo { get; set; }

    public string? TopCoverMatNo { get; set; }

    public string? ReelMatNo { get; set; }
}

public sealed class LocalTapeSetup
{
    public int? BeforeSpaceQty { get; set; }

    public int? PackageQty { get; set; }

    public int? AfterSpaceQty { get; set; }

    public int? SampleQty { get; set; }

    public int? BlankQty { get; set; }
}
