using System.Text;

namespace Kwy.Communicate.Gem;

/// <summary>将 GEM 接口目录导出为便于评审和版本管理的文档。</summary>
public static class GemInterfaceCatalogExporter
{
    public static string ToMarkdown(
        GemRegistry registry,
        string equipmentName,
        string? softwareVersion = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        string document = ToMarkdown(registry.Catalog, equipmentName);
        if (registry.AppliedProfile is not GemInterfaceProfileDescriptor profile)
            return document;

        var header = new StringBuilder();
        header.AppendLine($"# {Escape(equipmentName)} GEM Interface");
        header.AppendLine();
        header.AppendLine($"- Profile: `{Escape(profile.ProfileId)}`");
        header.AppendLine($"- Interface Version: `{Escape(profile.InterfaceVersion)}`");
        if (!string.IsNullOrWhiteSpace(softwareVersion))
            header.AppendLine($"- Software Version: `{Escape(softwareVersion)}`");
        if (!string.IsNullOrWhiteSpace(profile.ContentSha256))
            header.AppendLine($"- Profile SHA-256: `{profile.ContentSha256}`");
        header.AppendLine();

        int firstSection = document.IndexOf("## ", StringComparison.Ordinal);
        return firstSection < 0 ? header.ToString() : header.Append(document.AsSpan(firstSection)).ToString();
    }

    public static string ToMarkdown(GemInterfaceCatalog catalog, string equipmentName)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(equipmentName);

        var builder = new StringBuilder();
        builder.AppendLine($"# {Escape(equipmentName)} GEM Interface");
        builder.AppendLine();

        AppendVariables(builder, catalog);
        AppendConstants(builder, catalog);
        AppendReports(builder, catalog);
        AppendEvents(builder, catalog);
        AppendAlarms(builder, catalog);
        AppendRemoteCommands(builder, catalog);
        return builder.ToString();
    }

    private static void AppendVariables(StringBuilder builder, GemInterfaceCatalog catalog)
    {
        builder.AppendLine("## Variables");
        builder.AppendLine();
        builder.AppendLine("| VID | Name | Kind | Format | Unit | Description |");
        builder.AppendLine("|---:|---|---|---|---|---|");
        foreach (GemVariableDefinition item in catalog.Variables.Values.OrderBy(x => x.Vid.Value))
        {
            builder.AppendLine($"| {item.Vid.Value} | {Escape(item.Name)} | {item.Kind} | {item.Format?.ToString() ?? string.Empty} | {Escape(item.Unit)} | {Escape(item.Description)} |");
        }

        builder.AppendLine();
    }

    private static void AppendConstants(StringBuilder builder, GemInterfaceCatalog catalog)
    {
        builder.AppendLine("## Equipment Constants");
        builder.AppendLine();
        builder.AppendLine("| ECID | Name | Format | Unit | Description |");
        builder.AppendLine("|---:|---|---|---|---|");
        foreach (GemEquipmentConstantDefinition item in catalog.Constants.Values.OrderBy(x => x.Ecid.Value))
        {
            builder.AppendLine($"| {item.Ecid.Value} | {Escape(item.Name)} | {item.Format?.ToString() ?? string.Empty} | {Escape(item.Unit)} | {Escape(item.Description)} |");
        }

        builder.AppendLine();
    }

    private static void AppendReports(StringBuilder builder, GemInterfaceCatalog catalog)
    {
        builder.AppendLine("## Reports");
        builder.AppendLine();
        builder.AppendLine("| RPTID | VIDs |");
        builder.AppendLine("|---:|---|");
        foreach (GemReportDefinition item in catalog.Reports.Values.OrderBy(x => x.Rptid.Value))
        {
            builder.AppendLine($"| {item.Rptid.Value} | {string.Join(", ", item.VariableIds.Select(x => x.Value))} |");
        }

        builder.AppendLine();
    }

    private static void AppendEvents(StringBuilder builder, GemInterfaceCatalog catalog)
    {
        builder.AppendLine("## Collection Events");
        builder.AppendLine();
        builder.AppendLine("| CEID | Name | RPTIDs | Enabled |");
        builder.AppendLine("|---:|---|---|---|");
        foreach (GemCollectionEventDefinition item in catalog.Events.Values.OrderBy(x => x.Ceid.Value))
        {
            builder.AppendLine($"| {item.Ceid.Value} | {Escape(item.Name)} | {string.Join(", ", item.LinkedReports.Select(x => x.Value))} | {item.Enabled} |");
        }

        builder.AppendLine();
    }

    private static void AppendAlarms(StringBuilder builder, GemInterfaceCatalog catalog)
    {
        builder.AppendLine("## Alarms");
        builder.AppendLine();
        builder.AppendLine("| ALID | Code | Text | ALCD | Enabled |");
        builder.AppendLine("|---:|---|---|---:|---|");
        foreach (GemAlarmDefinition item in catalog.Alarms.Values.OrderBy(x => x.Alid.Value))
        {
            builder.AppendLine($"| {item.Alid.Value} | {Escape(item.Code)} | {Escape(item.Text)} | {item.AlarmCode} | {item.Enabled} |");
        }

        builder.AppendLine();
    }

    private static void AppendRemoteCommands(StringBuilder builder, GemInterfaceCatalog catalog)
    {
        builder.AppendLine("## Remote Commands");
        builder.AppendLine();
        builder.AppendLine("| RCMD | Handler Key | Parameters | Allowed States | Accepted CEID | Completed CEID | Failed CEID |");
        builder.AppendLine("|---|---|---|---|---:|---:|---:|");
        foreach (GemRemoteCommandDefinition item in catalog.RemoteCommands.Values.OrderBy(x => x.Name))
        {
            string parameters = string.Join(", ", item.Parameters.Select(parameter =>
                $"{parameter.Name}:{parameter.Format}{(parameter.Required ? "*" : string.Empty)}"));
            string states = item.AllowedControlStates is null
                ? string.Empty
                : string.Join(", ", item.AllowedControlStates.OrderBy(x => x));
            builder.AppendLine(
                $"| {Escape(item.Name)} | {Escape(item.EffectiveHandlerKey)} | {Escape(parameters)} | {Escape(states)} | {item.AcceptedEvent?.Value.ToString() ?? string.Empty} | {item.CompletedEvent?.Value.ToString() ?? string.Empty} | {item.FailedEvent?.Value.ToString() ?? string.Empty} |");
        }

        builder.AppendLine();
    }

    private static string Escape(string? value)
        => (value ?? string.Empty)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
