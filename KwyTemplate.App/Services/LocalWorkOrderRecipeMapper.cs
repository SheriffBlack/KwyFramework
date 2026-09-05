using KwyTemplate.App.Models;
using Kwy.Device.Abstractions;
using Kwy.Device.Instruments.Dcr;
using Kwy.Device.Instruments.Lcr;
using KwyTemplate.Device;
using KwyTemplate.MES.Abstract.Models;
using System.Text.Json;

namespace KwyTemplate.App.Services;

/// <summary>
/// 本地 JSON 配方与统一 MES 工单模型之间的唯一转换点。
/// 机型和仪表层继续只消费 MesWorkOrderSetup。
/// </summary>
public sealed class LocalWorkOrderRecipeMapper
{
    private const string PolarityStation1EnabledKey = "Polarity.Station1.Enabled";
    private const string PolarityStation2EnabledKey = "Polarity.Station2.Enabled";

    public LocalWorkOrderRecipe CreateEmpty(string machineType, string? machineProfileKey)
        => new()
        {
            EquipmentType = machineType,
            MachineProfileKey = machineProfileKey,
            Source = "Local",
            // Cyntec currently fixes StdPartsCheck at four hours.  This is a
            // recipe default, not an engineer-editable instrument parameter.
            StandardSampleCheckInterval = 4
        };

    public LocalWorkOrderRecipe FromMesSetup(MesWorkOrderSetup setup, string? machineProfileKey, string source = "MES")
    {
        ArgumentNullException.ThrowIfNull(setup);

        var recipe = new LocalWorkOrderRecipe
        {
            MachineProfileKey = machineProfileKey,
            Source = source,
            ProductNo = setup.ProductNo,
            ProductName = setup.ProductName,
            EquipmentType = setup.EquipmentType,
            StandardSampleCheckInterval = setup.StandardSampleCheckInterval,
            MaterialRequirements = setup.MaterialRequirements is null
                ? null
                : new LocalMaterialRequirements
                {
                    TablePaperMatNo = setup.MaterialRequirements.TablePaperMatNo,
                    TopCoverMatNo = setup.MaterialRequirements.TopCoverMatNo,
                    ReelMatNo = setup.MaterialRequirements.ReelMatNo
                },
            TapeSetup = setup.TapeSetup is null
                ? null
                : new LocalTapeSetup
                {
                    BeforeSpaceQty = setup.TapeSetup.BeforeSpaceQty,
                    PackageQty = setup.TapeSetup.PackageQty,
                    AfterSpaceQty = setup.TapeSetup.AfterSpaceQty,
                    SampleQty = setup.TapeSetup.SampleQty,
                    BlankQty = setup.TapeSetup.BlankQty
                }
        };

        foreach (MesParameterValue parameter in setup.Parameters.Values.Values)
        {
            if (!IsDerivedEnableParameter(parameter.Key) && !IsStructuredRecipeParameter(parameter.Key))
            {
                recipe.Parameters[parameter.Key] = parameter.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(setup.RecipeName))
        {
            recipe.Parameters["MatGroupNo"] = setup.RecipeName;
        }

        // MES 只有总开关 ZEnable。本地配方将其展开为两个可独立维护的工位源状态。
        bool zEnabled = IsEnabled(setup, "ZEnable");
        recipe.Parameters[PolarityStation1EnabledKey] = ToYesNo(zEnabled);
        recipe.Parameters[PolarityStation2EnabledKey] = ToYesNo(zEnabled);

        recipe.Measurements = [];
        foreach (MesWorkOrderInstrumentSetup item in setup.InstrumentSetups ?? [])
        {
            recipe.Measurements.Add(new LocalMeasurementSetup
            {
                ParameterId = item.ParameterId,
                DisplayName = item.DisplayName,
                LowerLimit = item.LowerLimit,
                UpperLimit = item.UpperLimit,
                Unit = item.Unit,
                Range = item.Range,
                Enabled = true
            });
        }

        return recipe;
    }

    public MesWorkOrderSetup ToMesSetup(LocalWorkOrderRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var parameters = new MesParameterBag();
        foreach ((string key, string value) in recipe.Parameters)
        {
            parameters.Set(key, value);
        }

        // Machine_4_HAHH still consumes these compatibility flags.  They are
        // derived from the enabled measurement items, rather than maintained
        // by a second set of manual switches.
        bool polarityStation1Enabled = IsPolarityStationEnabled(recipe, 1);
        bool polarityStation2Enabled = IsPolarityStationEnabled(recipe, 2);
        parameters.Set(PolarityStation1EnabledKey, ToYesNo(polarityStation1Enabled));
        parameters.Set(PolarityStation2EnabledKey, ToYesNo(polarityStation2Enabled));
        parameters.Set("ZEnable", ToYesNo(polarityStation1Enabled || polarityStation2Enabled));
        parameters.Set("QEnable", ToYesNo(HasEnabledMeasurement(recipe, "Q")));
        parameters.Set("LEnable2", ToYesNo(HasEnabledMeasurement(recipe, "LS2", "RS2", "Q2")));

        List<MesWorkOrderInstrumentSetup> instruments = CreateInstrumentSetups(recipe);
        AddInstrumentRuntimeParameters(parameters, recipe);

        List<MesMeasurementLimit> limits = instruments
            .Where(static item => item.LowerLimit.HasValue || item.UpperLimit.HasValue)
            .Select(static item => new MesMeasurementLimit(
                item.ParameterId,
                item.DisplayName,
                item.LowerLimit,
                item.UpperLimit,
                null,
                item.Unit))
            .ToList();

        return new MesWorkOrderSetup(
            string.Empty,
            recipe.ProductNo,
            recipe.ProductName,
            GetMatGroupNo(recipe),
            null,
            parameters,
            limits,
            DataSource: null,
            EquipmentType: recipe.EquipmentType,
            InstrumentSetups: instruments,
            MaterialRequirements: recipe.MaterialRequirements is null
                ? null
                : new MesWorkOrderMaterialRequirements(
                    recipe.MaterialRequirements.TablePaperMatNo,
                    recipe.MaterialRequirements.TopCoverMatNo,
                    recipe.MaterialRequirements.ReelMatNo),
            TapeSetup: recipe.TapeSetup is null
                ? null
                : new MesWorkOrderTapeSetup(
                    recipe.TapeSetup.BeforeSpaceQty,
                    recipe.TapeSetup.PackageQty,
                    recipe.TapeSetup.AfterSpaceQty,
                    recipe.TapeSetup.SampleQty,
                    recipe.TapeSetup.BlankQty,
                    recipe.TapeSetup.BlankQty),
            StandardSampleCheckInterval: recipe.StandardSampleCheckInterval);
    }

    /// <summary>
    /// 将 SetView 中一个仪表的已编辑配置投影回统一配方。
    /// 不调用仪表通讯，因此可在写仪表失败时仍保留现场输入。
    /// </summary>
    public bool UpdateFromInstrumentConfig(
        LocalWorkOrderRecipe recipe,
        IDeviceConfig config,
        IReadOnlyCollection<string> stationParameterIds,
        int? stationId = null,
        string? deviceId = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(config);

        CaptureInstrumentConfig(recipe, deviceId, config);
        // 从此处开始已具备与 DeviceCatalog 同构的仪表快照；旧 Measurements
        // 仅用于读取历史 JSON，不能再被新的工位应用重新写回。
        bool useInstrumentSnapshot = !string.IsNullOrWhiteSpace(deviceId);

        switch (config)
        {
            case AdexDcrConfig dcr:
                if (useInstrumentSnapshot)
                {
                    return true;
                }

                string dcrId = stationParameterIds.FirstOrDefault(static value => value.StartsWith("DCR", StringComparison.OrdinalIgnoreCase)) ?? "DCR1";
                UpsertMeasurement(recipe, dcrId, dcrId, dcr.LowerLimit, dcr.UpperLimit, dcr.LowerLimitUnit, dcr.Range);
                return true;

            case HiokiLcrConfig lcr:
                if (useInstrumentSnapshot)
                {
                    SetPolarityStationEnabled(recipe, stationId, !lcr.IsMeasurementDisabled);
                    return true;
                }

                SaveHiokiLcrSettings(recipe, lcr, stationId);
                if (lcr.IsMeasurementDisabled)
                {
                    IEnumerable<LocalMeasurementSetup> measurements = stationId is 1 or 2
                        ? GetMeasurements(recipe).Where(item => IsPolarityMeasurementForStation(item.ParameterId, stationId.Value))
                        : GetMeasurements(recipe).Where(item => stationParameterIds.Contains(item.ParameterId, StringComparer.OrdinalIgnoreCase));
                    foreach (LocalMeasurementSetup measurement in measurements)
                    {
                        measurement.Enabled = false;
                    }

                    SetPolarityStationEnabled(recipe, stationId, false);

                    return true;
                }

                HiokiLcrMeasurementSettings active = lcr.GetActiveMeasurementSettings();
                HiokiLcrParameterPair pair = active.Parameters;
                string parameter1Id = NormalizeMeasurementId(pair.Parameter1, stationId);
                string parameter3Id = NormalizeMeasurementId(pair.Parameter3, stationId);
                if (lcr.IsDualFrequencyEnabled)
                {
                    parameter1Id += "2";
                    parameter3Id += "2";
                }

                UpsertMeasurement(recipe, parameter1Id, parameter1Id, active.PrimaryLimit.Minimum, active.PrimaryLimit.Maximum, active.PrimaryLimit.MinimumUnit, lcr.Range);
                UpsertMeasurement(recipe, parameter3Id, parameter3Id, active.SecondaryLimit.Minimum, active.SecondaryLimit.Maximum, active.SecondaryLimit.MinimumUnit, lcr.Range);
                recipe.Parameters[$"{parameter1Id}.Frequency"] = active.Frequency.ToString(System.Globalization.CultureInfo.InvariantCulture);
                recipe.Parameters[$"{parameter1Id}.FrequencyUnit"] = active.FrequencyUnit;
                recipe.Parameters[$"{parameter3Id}.Frequency"] = active.Frequency.ToString(System.Globalization.CultureInfo.InvariantCulture);
                recipe.Parameters[$"{parameter3Id}.FrequencyUnit"] = active.FrequencyUnit;
                SetPolarityStationEnabled(recipe, stationId, true);
                return true;

            default:
                return false;
        }
    }

    private static bool HasEnabledMeasurement(LocalWorkOrderRecipe recipe, params string[] parameterPrefixes)
        => GetMeasurements(recipe).Any(item => item.Enabled
            && parameterPrefixes.Any(prefix => item.ParameterId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

    private static string ToYesNo(bool enabled) => enabled ? "Y" : "N";

    private static bool IsEnabled(MesWorkOrderSetup setup, string key)
        => setup.Parameters.TryGetString(key, out string value)
            && value.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase);

    private static bool IsPolarityStationEnabled(LocalWorkOrderRecipe recipe, int stationId)
    {
        string key = stationId == 1 ? PolarityStation1EnabledKey : PolarityStation2EnabledKey;
        if (recipe.Parameters.TryGetValue(key, out string? value))
        {
            return value.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase);
        }

        // 兼容旧 JSON：未保存独立开关时，从该工位的测量项推导。
        return HasEnabledMeasurement(recipe, $"Z{stationId}", $"PHASE{stationId}");
    }

    private static void SetPolarityStationEnabled(LocalWorkOrderRecipe recipe, int? stationId, bool enabled)
    {
        if (stationId == 1)
        {
            recipe.Parameters[PolarityStation1EnabledKey] = ToYesNo(enabled);
        }
        else if (stationId == 2)
        {
            recipe.Parameters[PolarityStation2EnabledKey] = ToYesNo(enabled);
        }
    }

    private static bool IsPolarityMeasurementForStation(string parameterId, int stationId)
        => string.Equals(parameterId, $"Z{stationId}", StringComparison.OrdinalIgnoreCase)
           || string.Equals(parameterId, $"PHASE{stationId}", StringComparison.OrdinalIgnoreCase)
           // HIOKI Z-θ protocol uses PHAS internally; local recipes use the
           // canonical PHASE{stationId} identifier.
           || string.Equals(parameterId, $"PHAS{stationId}", StringComparison.OrdinalIgnoreCase);

    private static void UpsertMeasurement(
        LocalWorkOrderRecipe recipe,
        string parameterId,
        string displayName,
        double lowerLimit,
        double upperLimit,
        string? unit,
        string? range)
    {
        List<LocalMeasurementSetup> measurements = GetMeasurements(recipe);
        LocalMeasurementSetup? measurement = measurements.FirstOrDefault(item => string.Equals(item.ParameterId, parameterId, StringComparison.OrdinalIgnoreCase));
        if (measurement == null)
        {
            measurement = new LocalMeasurementSetup { ParameterId = parameterId };
            measurements.Add(measurement);
        }

        measurement.DisplayName = displayName;
        measurement.Enabled = true;
        measurement.LowerLimit = lowerLimit;
        measurement.UpperLimit = upperLimit;
        measurement.Unit = unit;
        measurement.Range = range;
    }

    public void CaptureInstrumentConfig(LocalWorkOrderRecipe recipe, string? deviceId, IDeviceConfig config)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        recipe.InstrumentConfigs[deviceId] = JsonSerializer.SerializeToElement(config, config.GetType());
    }

    public void CaptureInstrumentConfigs(
        LocalWorkOrderRecipe recipe,
        IEnumerable<IDevice> devices,
        IEnumerable<string> instrumentDeviceIds)
    {
        var ids = new HashSet<string>(instrumentDeviceIds, StringComparer.OrdinalIgnoreCase);
        foreach (IDevice device in devices)
        {
            if (ids.Contains(device.DeviceId) && device is IConfigurableDevice configurable)
            {
                CaptureInstrumentConfig(recipe, device.DeviceId, configurable.DeviceParameter);
                CapturePolarityStationEnabled(recipe, device.DeviceId, configurable.DeviceParameter);
            }
        }
    }

    /// <summary>
    /// 本地配方只保存仪表快照时，极性工位的开关也必须随 HIOKI 配置一并落盘。
    /// 否则旧版 Measurements 已移除后，离线加载会把未显式保存的极性工位误判为关闭。
    /// </summary>
    private static void CapturePolarityStationEnabled(
        LocalWorkOrderRecipe recipe,
        string deviceId,
        IDeviceConfig config)
    {
        if (config is not HiokiLcrConfig lcr)
        {
            return;
        }

        if (string.Equals(deviceId, DeviceIds.Instrument("Pol", 1), StringComparison.OrdinalIgnoreCase))
        {
            SetPolarityStationEnabled(recipe, 1, !lcr.IsMeasurementDisabled);
        }
        else if (string.Equals(deviceId, DeviceIds.Instrument("Pol", 2), StringComparison.OrdinalIgnoreCase))
        {
            SetPolarityStationEnabled(recipe, 2, !lcr.IsMeasurementDisabled);
        }
    }

    /// <summary>快照齐全后移除旧版工单中重复的仪表/MES 参数字段。</summary>
    public void RemoveRedundantRecipeData(LocalWorkOrderRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        recipe.SchemaVersion = 2;
        if (!recipe.Parameters.ContainsKey("MatGroupNo") && !string.IsNullOrWhiteSpace(recipe.LegacyRecipeName))
        {
            recipe.Parameters["MatGroupNo"] = recipe.LegacyRecipeName;
        }

        recipe.LegacyRecipeName = null;
        recipe.LegacyRecipeRevision = null;
        recipe.Measurements = null;
        foreach (string key in recipe.Parameters.Keys
                     .Where(key => IsLegacyInstrumentParameter(key) || IsStructuredRecipeParameter(key))
                     .ToArray())
        {
            recipe.Parameters.Remove(key);
        }
    }

    private static bool IsLegacyInstrumentParameter(string key)
        => key.StartsWith("DCR", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("RS", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("ZMaxValue", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("ZMinValue", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("Theta", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("QMaxValue", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("QMinValue", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("LMaxValue", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("LMinValue", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("LUnit", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("LFreq", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("LCRRange", StringComparison.OrdinalIgnoreCase)
           || key.EndsWith(".Frequency", StringComparison.OrdinalIgnoreCase)
           || key.EndsWith(".FrequencyUnit", StringComparison.OrdinalIgnoreCase)
           || key.StartsWith("Instrument.Station", StringComparison.OrdinalIgnoreCase);

    private static bool IsStructuredRecipeParameter(string key)
        => key is "TablePaperMatNo" or "TopCoverMatNo" or "ReelMatNo"
            or "BeforeSpaceQty" or "PackageQty" or "AfterSpaceQty" or "SampleQty" or "BlankQty"
            or "StdPartsCheck";

    private static string? GetMatGroupNo(LocalWorkOrderRecipe recipe)
        => recipe.Parameters.TryGetValue("MatGroupNo", out string? matGroupNo)
            ? matGroupNo
            : recipe.LegacyRecipeName;

    private static List<LocalMeasurementSetup> GetMeasurements(LocalWorkOrderRecipe recipe)
        => recipe.Measurements ??= [];

    private static List<MesWorkOrderInstrumentSetup> CreateInstrumentSetups(LocalWorkOrderRecipe recipe)
    {
        var setups = GetMeasurements(recipe)
            .Where(static item => item.Enabled)
            .Select(static item => new MesWorkOrderInstrumentSetup(item.ParameterId, string.IsNullOrWhiteSpace(item.DisplayName) ? item.ParameterId : item.DisplayName, item.LowerLimit, item.UpperLimit, item.Unit, item.Range))
            .ToList();
        foreach ((string deviceId, JsonElement element) in recipe.InstrumentConfigs)
        {
            if (deviceId.Contains(".Dcr.", StringComparison.OrdinalIgnoreCase))
            {
                AdexDcrConfig? dcrConfig = element.Deserialize<AdexDcrConfig>();
                if (dcrConfig != null)
                {
                    RemoveSetups(setups, "DCR1");
                    setups.Add(new("DCR1", "DCR1", dcrConfig.LowerLimit, dcrConfig.UpperLimit, dcrConfig.LowerLimitUnit, dcrConfig.Range));
                }

                continue;
            }

            if (!IsHiokiLcrDeviceId(deviceId))
            {
                continue;
            }

            HiokiLcrConfig? config = element.Deserialize<HiokiLcrConfig>();
            if (config == null)
            {
                continue;
            }

            int? stationId = GetStationId(deviceId);
            bool dual = string.Equals(config.FrequencyMode, HiokiLcrConfig.DualFrequency, StringComparison.Ordinal);
            HiokiLcrParameterPair pair = HiokiLcrLoadTypes.Resolve(dual ? config.SecondLoadType : config.LoadType);
            if (pair.Parameter1 == "OFF" && pair.Parameter3 == "OFF")
            {
                RemoveSetups(setups, GetOwnedParameterIds(deviceId));
                continue;
            }

            string suffix = dual ? "2" : string.Empty;
            string parameter1Id = NormalizeMeasurementId(pair.Parameter1, stationId) + suffix;
            string parameter3Id = NormalizeMeasurementId(pair.Parameter3, stationId) + suffix;
            RemoveSetups(setups, GetOwnedParameterIds(deviceId));
            AddHiokiSetup(setups, parameter1Id, dual ? config.SecondParameter1LowerLimit : config.Parameter1LowerLimit, dual ? config.SecondParameter1UpperLimit : config.Parameter1UpperLimit, dual ? config.SecondParameter1LowerLimitUnit : config.Parameter1LowerLimitUnit, config.Range);
            AddHiokiSetup(setups, parameter3Id, dual ? config.SecondParameter3LowerLimit : config.Parameter3LowerLimit, dual ? config.SecondParameter3UpperLimit : config.Parameter3UpperLimit, dual ? config.SecondParameter3LowerLimitUnit : config.Parameter3LowerLimitUnit, config.Range);
        }

        return setups;
    }

    private static void AddHiokiSetup(List<MesWorkOrderInstrumentSetup> setups, string parameterId, double lower, double upper, string unit, string range)
        => setups.Add(new(parameterId, parameterId, lower, upper, unit, range));

    private static void RemoveSetups(List<MesWorkOrderInstrumentSetup> setups, params string[] parameterIds)
        => setups.RemoveAll(item => parameterIds.Contains(item.ParameterId, StringComparer.OrdinalIgnoreCase));

    private static string[] GetOwnedParameterIds(string deviceId)
        => deviceId.Contains(".Dcr.", StringComparison.OrdinalIgnoreCase) ? ["DCR1"]
            : deviceId.Contains(".Pol.01", StringComparison.OrdinalIgnoreCase) ? ["Z1", "PHASE1", "PHAS1"]
            : deviceId.Contains(".Pol.02", StringComparison.OrdinalIgnoreCase) ? ["Z2", "PHASE2", "PHAS2"]
            : deviceId.Contains(".Ind.01", StringComparison.OrdinalIgnoreCase) ? ["Ls", "Rs", "Q", "Ls2", "Rs2", "Q2"]
            : [];

    private static int? GetStationId(string deviceId)
        => deviceId.Contains(".Pol.01", StringComparison.OrdinalIgnoreCase) ? 1
            : deviceId.Contains(".Pol.02", StringComparison.OrdinalIgnoreCase) ? 2
            : deviceId.Contains(".Ind.01", StringComparison.OrdinalIgnoreCase) ? 4
            : null;

    private static void AddInstrumentRuntimeParameters(MesParameterBag parameters, LocalWorkOrderRecipe recipe)
    {
        foreach ((string deviceId, JsonElement element) in recipe.InstrumentConfigs)
        {
            if (!IsHiokiLcrDeviceId(deviceId) || GetStationId(deviceId) is not int stationId)
            {
                continue;
            }

            HiokiLcrConfig? config = element.Deserialize<HiokiLcrConfig>();
            if (config == null)
            {
                continue;
            }

            Set(HiokiLcrRecipeKeys.FrequencyMode, config.FrequencyMode);
            Set(HiokiLcrRecipeKeys.LoadType, config.LoadType);
            Set(HiokiLcrRecipeKeys.Frequency, config.Frequency);
            Set(HiokiLcrRecipeKeys.FrequencyUnit, config.FrequencyUnit);
            Set(HiokiLcrRecipeKeys.Voltage, config.Voltage);
            Set(HiokiLcrRecipeKeys.VoltageUnit, config.VoltageUnit);
            Set(HiokiLcrRecipeKeys.Delay, config.Delay);
            Set(HiokiLcrRecipeKeys.Range, config.Range);
            Set(HiokiLcrRecipeKeys.Speed, config.Speed);
            Set(HiokiLcrRecipeKeys.SecondLoadType, config.SecondLoadType);
            Set(HiokiLcrRecipeKeys.Frequency2, config.Frequency2);
            Set(HiokiLcrRecipeKeys.Frequency2Unit, config.Frequency2Unit);

            void Set(string name, object value) => parameters.Set(HiokiLcrRecipeKeys.Get(stationId, name), value);
        }
    }

    private static bool IsHiokiLcrDeviceId(string deviceId)
        => deviceId.Contains(".Pol.", StringComparison.OrdinalIgnoreCase)
           || deviceId.Contains(".Ind.", StringComparison.OrdinalIgnoreCase);

    private static void SaveHiokiLcrSettings(LocalWorkOrderRecipe recipe, HiokiLcrConfig config, int? stationId)
    {
        if (stationId is not > 0)
        {
            return;
        }

        int id = stationId.Value;
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.FrequencyMode)] = config.FrequencyMode;
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.LoadType)] = config.LoadType;
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.Frequency)] = config.Frequency.ToString(System.Globalization.CultureInfo.InvariantCulture);
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.FrequencyUnit)] = config.FrequencyUnit;
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.Voltage)] = config.Voltage.ToString(System.Globalization.CultureInfo.InvariantCulture);
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.VoltageUnit)] = config.VoltageUnit;
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.Delay)] = config.Delay.ToString(System.Globalization.CultureInfo.InvariantCulture);
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.Range)] = config.Range;
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.Speed)] = config.Speed;
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.SecondLoadType)] = config.SecondLoadType;
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.Frequency2)] = config.Frequency2.ToString(System.Globalization.CultureInfo.InvariantCulture);
        recipe.Parameters[HiokiLcrRecipeKeys.Get(id, HiokiLcrRecipeKeys.Frequency2Unit)] = config.Frequency2Unit;
    }

    private static string NormalizeMeasurementId(string parameterId, int? stationId = null)
        => parameterId.Trim().ToUpperInvariant() switch
        {
            "L_S" or "LS" => "Ls",
            "R_S" or "RS" => "Rs",
            "Z" when stationId is > 0 => $"Z{stationId.Value}",
            "THETA" or "PHAS" or "PHASE" or "θ" when stationId is > 0 => $"PHASE{stationId.Value}",
            "THETA" or "PHAS" or "PHASE" or "θ" => "PHASE",
            _ => parameterId.Trim().ToUpperInvariant()
        };

    private static bool IsDerivedEnableParameter(string key)
        => key is "ZEnable" or "QEnable" or "LEnable2"
            or PolarityStation1EnabledKey or PolarityStation2EnabledKey
            or "Polarity.Enabled" or "Q.Enabled" or "Inductance.SecondFrequency.Enabled";
}
