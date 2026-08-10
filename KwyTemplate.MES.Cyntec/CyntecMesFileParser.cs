using System.Globalization;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.MES.Cyntec;

internal static class CyntecMesFileParser
{
    public static MesWorkOrderSetup ParseWorkOrderSetup(string workOrderNo, string filePath)
    {
        MesParameterBag bag = ReadKeyValueFile(filePath);
        List<MesWorkOrderInstrumentSetup> instrumentSetups = CreateInstrumentSetups(bag);
        var limits = new List<MesMeasurementLimit>();

        foreach (MesWorkOrderInstrumentSetup item in instrumentSetups)
        {
            if (item.LowerLimit.HasValue || item.UpperLimit.HasValue)
            {
                limits.Add(new MesMeasurementLimit(item.ParameterId, item.DisplayName, item.LowerLimit, item.UpperLimit, null, item.Unit));
            }
        }

        if (IsEnabled(bag, "QEnable"))
        {
            AddLimit(limits, bag, "Q", "Q", "QMinValue", "QMaxValue", "QStandardValue", null);
        }

        // LEnable2 belongs to the optional second inductance parameter set
        // (LMinValue2/LMaxValue2). It must not suppress the primary Ls/Rs setup.
        AddLimit(limits, bag, "LS", "LS", "LMinValue", "LMaxValue", "LStandardValue", "LUnit");
        AddLimit(limits, bag, "RS", "RS", "RSMinValue", "RSMaxValue", "RSStandardValue", "RSUnit");

        var dataSource = CreateFileSource(filePath, "key-value-csv");
        string? productNo = TryGetString(bag, "MatNo") ?? TryGetString(bag, "ProductNo") ?? TryGetString(bag, "PartNumber");
        string? productName = TryGetString(bag, "ProductName");
        string? equipmentType = TryGetString(bag, "EquipmentType");
        string? recipeName = TryGetString(bag, "RecipeName") ?? TryGetString(bag, "MatGroupNo");
        string? recipeRevision = TryGetString(bag, "RecipeRevision");
        var materialRequirements = new MesWorkOrderMaterialRequirements(
            TryGetString(bag, "TablePaperMatNo"),
            TryGetString(bag, "TopCoverMatNo"),
            TryGetString(bag, "ReelMatNo"));
        var tapeSetup = new MesWorkOrderTapeSetup(
            TryGetInt32(bag, "BeforeSpaceQty"),
            TryGetInt32(bag, "PackageQty"),
            TryGetInt32(bag, "AfterSpaceQty"),
            TryGetInt32(bag, "SampleQty"),
            TryGetInt32(bag, "BlankQty"),
            TryGetInt32(bag, "BlankQty"));
        int? standardSampleCheckInterval = TryGetInt32(bag, "StdPartsCheck");

        return new MesWorkOrderSetup(
            workOrderNo,
            productNo,
            productName,
            recipeName,
            recipeRevision,
            bag,
            limits,
            dataSource,
            equipmentType,
            instrumentSetups,
            materialRequirements,
            tapeSetup,
            standardSampleCheckInterval);
    }

    public static MesStandardSampleSetup ParseStandardSampleSetup(string workOrderNo, string? sampleCode, string filePath)
    {
        var limits = new List<MesMeasurementLimit>();
        MesParameterBag bag = new();
        bool hasPrimarySampleDates = false;

        if (!File.Exists(filePath))
        {
            return new MesStandardSampleSetup(workOrderNo, sampleCode, limits, bag, CreateFileSource(filePath, "standard-sample-csv"));
        }

        foreach (string line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] parts = line.Split(',');
            if (parts.Length < 13)
            {
                continue;
            }

            string rawParameterId = parts[6].Trim();
            if (string.IsNullOrWhiteSpace(rawParameterId))
            {
                continue;
            }

            string rawDisplayName = NormalizeEmpty(parts[5]) ?? rawParameterId;
            string parameterId = NormalizeStandardSampleParameterId(rawParameterId, rawDisplayName);
            string displayName = parameterId;
            double? standard = TryParseDouble(parts[7]);
            double? upper = TryParseDouble(parts[8]);
            double? lower = TryParseDouble(parts[9]);
            string? unit = NormalizeEmpty(parts[10]);
            limits.Add(new MesMeasurementLimit(
                parameterId,
                displayName,
                lower,
                upper,
                standard,
                unit,
                SerialNo: NormalizeEmpty(parts[0]),
                MeterType: NormalizeEmpty(parts[6]),
                ItemName: NormalizeEmpty(parts[5]),
                Frequency: NormalizeEmpty(parts[11]),
                FrequencyUnit: NormalizeEmpty(parts[12])));

            if (!hasPrimarySampleDates)
            {
                bag.Set("IssueDate", parts[1].Trim());
                bag.Set("ExpireDate", parts[3].Trim());
                hasPrimarySampleDates = true;
            }

            string prefix = $"Standard.{parameterId}.";
            bag.Set(prefix + "SerialNo", parts[0].Trim());
            bag.Set(prefix + "IssueDate", parts[1].Trim());
            bag.Set(prefix + "ValidDays", parts[2].Trim());
            bag.Set(prefix + "ExpireDate", parts[3].Trim());
            bag.Set(prefix + "IsValid", parts[4].Trim());
            bag.Set(prefix + "ItemName", parts[5].Trim());
            bag.Set(prefix + "MeterType", parts[6].Trim());
            bag.Set(prefix + "CenterValue", parts[7].Trim());
            bag.Set(prefix + "UpperLimit", parts[8].Trim());
            bag.Set(prefix + "LowerLimit", parts[9].Trim());
            bag.Set(prefix + "Unit", parts[10].Trim());
            bag.Set(prefix + "Frequency", parts[11].Trim());
            bag.Set(prefix + "FrequencyUnit", parts[12].Trim());
        }

        return new MesStandardSampleSetup(workOrderNo, sampleCode, limits, bag, CreateFileSource(filePath, "standard-sample-csv"));
    }

    private static string NormalizeStandardSampleParameterId(string meterType, string itemName)
    {
        string normalizedMeterType = meterType.Trim().ToUpperInvariant();
        string normalizedItemName = itemName.Trim().ToUpperInvariant();

        if (normalizedMeterType == "LCR" || normalizedItemName.StartsWith("LCR", StringComparison.OrdinalIgnoreCase))
        {
            return "LS";
        }

        if (normalizedMeterType.StartsWith("RS", StringComparison.OrdinalIgnoreCase) || normalizedItemName.StartsWith("RS", StringComparison.OrdinalIgnoreCase))
        {
            return "RS";
        }

        if (normalizedMeterType.StartsWith("DCR", StringComparison.OrdinalIgnoreCase) || normalizedItemName.StartsWith("DCR", StringComparison.OrdinalIgnoreCase))
        {
            return "DCR";
        }

        return normalizedMeterType;
    }
    private static List<MesWorkOrderInstrumentSetup> CreateInstrumentSetups(MesParameterBag bag)
    {
        string? dcrRange = TryGetString(bag, "DCRRange");
        string? dcrUnit = TryGetString(bag, "DCRUnit");
        var items = new List<MesWorkOrderInstrumentSetup>
        {
            new(
                "DCR1",
                "DCR1",
                TryGetDouble(bag, "DCRMinValue"),
                TryGetDouble(bag, "DCRMaxValue"),
                dcrUnit,
                dcrRange),
            new(
                "DCR2",
                "DCR2",
                TryGetDouble(bag, "DCRMinValue2"),
                TryGetDouble(bag, "DCRMaxValue2"),
                TryGetString(bag, "DCRUnit2") ?? dcrUnit,
                TryGetString(bag, "DCRRange2") ?? dcrRange)
        };

        if (IsEnabled(bag, "ZEnable"))
        {
            items.Add(new("Z1", "Z1", TryGetDouble(bag, "ZMinValue1"), TryGetDouble(bag, "ZMaxValue1"), "mΩ", TryGetString(bag, "LCRRange")));
            items.Add(new("PHASE1", "PHASE1", TryGetDouble(bag, "ThetaMinValue1"), TryGetDouble(bag, "ThetaMaxValue1"), "°", TryGetString(bag, "LCRRange")));
            items.Add(new("Z2", "Z2", TryGetDouble(bag, "ZMinValue2"), TryGetDouble(bag, "ZMaxValue2"), "mΩ", TryGetString(bag, "LCRRange")));
            items.Add(new("PHASE2", "PHASE2", TryGetDouble(bag, "ThetaMinValue2"), TryGetDouble(bag, "ThetaMaxValue2"), "°", TryGetString(bag, "LCRRange")));
        }

        if (IsEnabled(bag, "QEnable"))
        {
            items.Add(new("Q", "Q", TryGetDouble(bag, "QMinValue"), TryGetDouble(bag, "QMaxValue"), null, TryGetString(bag, "LCRRange")));
        }

        // The primary Hioki Ls/Rs setup is defined by LMin/MaxValue and RSMin/MaxValue.
        // LEnable2 only describes the separate *2 parameter set and cannot gate these values.
        string? lcrRange = TryGetString(bag, "LCRRange");
        items.Add(new("Ls", "Ls", TryGetDouble(bag, "LMinValue"), TryGetDouble(bag, "LMaxValue"), TryGetString(bag, "LUnit"), lcrRange));
        items.Add(new("Rs", "Rs", TryGetDouble(bag, "RSMinValue"), TryGetDouble(bag, "RSMaxValue"), TryGetString(bag, "RSUnit"), lcrRange));

        return items
            .Where(static item => item.LowerLimit.HasValue || item.UpperLimit.HasValue || !string.IsNullOrWhiteSpace(item.Unit) || !string.IsNullOrWhiteSpace(item.Range))
            .ToList();
    }

    private static MesParameterBag ReadKeyValueFile(string filePath)
    {
        MesParameterBag bag = new();
        if (!File.Exists(filePath))
        {
            return bag;
        }

        foreach (string line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] parts = line.Split(',', 3);
            string key;
            string value;
            if (parts.Length >= 3)
            {
                key = parts[1].Trim();
                value = parts[2].Trim();
            }
            else if (parts.Length == 2)
            {
                key = NormalizeKey(parts[0]);
                value = parts[1].Trim();
            }
            else
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                bag.Set(key, value);
            }
        }

        return bag;
    }

    private static string NormalizeKey(string value)
    {
        string key = value.Trim();
        int dotIndex = key.IndexOf('.');
        return dotIndex >= 0 && dotIndex < key.Length - 1 ? key[(dotIndex + 1)..].Trim() : key;
    }

    private static void AddLimit(List<MesMeasurementLimit> limits, MesParameterBag bag, string parameterId, string displayName, string lowerKey, string upperKey, string standardKey, string? unitKey)
    {
        double? lower = TryGetDouble(bag, lowerKey);
        double? upper = TryGetDouble(bag, upperKey);
        double? standard = TryGetDouble(bag, standardKey);
        string? unit = unitKey == null ? null : TryGetString(bag, unitKey);

        if (lower.HasValue || upper.HasValue || standard.HasValue)
        {
            limits.Add(new MesMeasurementLimit(parameterId, displayName, lower, upper, standard, unit));
        }
    }

    private static MesExternalDataSource CreateFileSource(string filePath, string format)
    {
        DateTimeOffset? lastWriteTime = File.Exists(filePath) ? File.GetLastWriteTime(filePath) : null;
        return new MesExternalDataSource(MesExternalDataSourceKind.File, filePath, format, lastWriteTime);
    }

    private static bool IsEnabled(MesParameterBag bag, string key)
        => !bag.TryGetString(key, out string value)
            || value.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase);
    private static string? TryGetString(MesParameterBag bag, string key)
        => bag.TryGetString(key, out string value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static int? TryGetInt32(MesParameterBag bag, string key)
        => bag.TryGetInt32(key, out int value) ? value : null;

    private static double? TryGetDouble(MesParameterBag bag, string key)
        => bag.TryGetDouble(key, out double value) ? value : null;

    private static double? TryParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            || double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out result)
            ? result
            : null;
    }

    private static string? NormalizeEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}


