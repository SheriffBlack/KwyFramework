using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>识别一份随软件发布的 GEM 接口合同。</summary>
public sealed record GemInterfaceProfileDescriptor(
    string ProfileId,
    string InterfaceVersion,
    string Source,
    string? ContentSha256 = null,
    string? Customer = null,
    string? EquipmentModel = null);

/// <summary>将软件版本与已审核的接口 Profile 绑定在同一发布记录中。</summary>
public sealed record GemInterfaceReleaseManifest(
    string SoftwareVersion,
    GemInterfaceProfileDescriptor Profile,
    DateTimeOffset CreatedAt);

/// <summary>
/// 将 C# 或外部文件中的接口定义应用到统一的 <see cref="GemInterfaceCatalog"/>。
/// Profile 只允许声明接口，不会从文件加载或执行业务代码。
/// </summary>
public interface IGemInterfaceProfile
{
    GemInterfaceProfileDescriptor Descriptor { get; }

    void ApplyTo(GemInterfaceCatalog catalog);
}

/// <summary>用于代码内固定接口合同的 C# Profile 基类。</summary>
public abstract class GemInterfaceProfile : IGemInterfaceProfile
{
    protected GemInterfaceProfile(
        string profileId,
        string interfaceVersion,
        string? customer = null,
        string? equipmentModel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceVersion);
        Descriptor = new GemInterfaceProfileDescriptor(
            profileId,
            interfaceVersion,
            "CSharp",
            Customer: customer,
            EquipmentModel: equipmentModel);
    }

    public GemInterfaceProfileDescriptor Descriptor { get; }

    public void ApplyTo(GemInterfaceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Configure(catalog);
    }

    protected abstract void Configure(GemInterfaceCatalog catalog);
}

/// <summary>JSON Profile 的可版本化文档模型。</summary>
public sealed record GemJsonInterfaceProfileDocument
{
    public int SchemaVersion { get; init; } = 1;

    public required string ProfileId { get; init; }

    public required string InterfaceVersion { get; init; }

    public string? Customer { get; init; }

    public string? EquipmentModel { get; init; }

    public IReadOnlyList<GemJsonVariableDefinition> Variables { get; init; } = [];

    public IReadOnlyList<GemJsonConstantDefinition> Constants { get; init; } = [];

    public IReadOnlyList<GemJsonReportDefinition> Reports { get; init; } = [];

    public IReadOnlyList<GemJsonEventDefinition> Events { get; init; } = [];

    public IReadOnlyList<GemJsonAlarmDefinition> Alarms { get; init; } = [];

    public IReadOnlyList<GemJsonRemoteCommandDefinition> RemoteCommands { get; init; } = [];
}

public sealed record GemJsonVariableDefinition(
    uint Id,
    string Name,
    GemVariableKind Kind,
    string? Unit = null,
    string? Description = null,
    SecsFormat? Format = null);

public sealed record GemJsonConstantDefinition(
    uint Id,
    string Name,
    string? Unit = null,
    string? Description = null,
    SecsFormat? Format = null);

public sealed record GemJsonReportDefinition(uint Id, IReadOnlyList<uint> VariableIds);

public sealed record GemJsonEventDefinition(
    uint Id,
    string Name,
    IReadOnlyList<uint> ReportIds,
    bool Enabled = true);

public sealed record GemJsonAlarmDefinition(
    uint Id,
    string Code,
    string Text,
    byte AlarmCode = 0,
    bool Enabled = true);

public sealed record GemJsonRemoteCommandParameterDefinition(
    string Name,
    SecsFormat Format,
    bool Required = true);

public sealed record GemJsonRemoteCommandDefinition(
    string Name,
    IReadOnlyList<GemJsonRemoteCommandParameterDefinition> Parameters,
    IReadOnlyList<GemControlState>? AllowedControlStates = null,
    bool AllowAdditionalParameters = false,
    uint? AcceptedEventId = null,
    uint? CompletedEventId = null,
    uint? FailedEventId = null,
    string? HandlerKey = null);

/// <summary>安全地将 JSON 接口合同转换为运行时 Catalog。</summary>
public sealed class GemJsonInterfaceProfile : IGemInterfaceProfile
{
    internal GemJsonInterfaceProfile(GemJsonInterfaceProfileDocument document, string contentSha256)
    {
        Document = document;
        Descriptor = new GemInterfaceProfileDescriptor(
            document.ProfileId,
            document.InterfaceVersion,
            "Json",
            contentSha256,
            document.Customer,
            document.EquipmentModel);
    }

    public GemJsonInterfaceProfileDocument Document { get; }

    public GemInterfaceProfileDescriptor Descriptor { get; }

    public void ApplyTo(GemInterfaceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        foreach (GemJsonVariableDefinition item in Document.Variables)
            catalog.RegisterVariable(new(new GemVid(item.Id), item.Name, item.Kind, item.Unit, item.Description, item.Format));
        foreach (GemJsonConstantDefinition item in Document.Constants)
            catalog.RegisterConstant(new(new GemEcid(item.Id), item.Name, item.Unit, item.Description, item.Format));
        foreach (GemJsonReportDefinition item in Document.Reports)
            catalog.RegisterReport(new(new GemRptid(item.Id), item.VariableIds.Select(id => new GemVid(id)).ToArray()));
        foreach (GemJsonEventDefinition item in Document.Events)
            catalog.RegisterEvent(new(new GemCeid(item.Id), item.Name, item.ReportIds.Select(id => new GemRptid(id)).ToArray(), item.Enabled));
        foreach (GemJsonAlarmDefinition item in Document.Alarms)
            catalog.RegisterAlarm(new(new GemAlid(item.Id), item.Code, item.Text, item.AlarmCode, item.Enabled));
        foreach (GemJsonRemoteCommandDefinition item in Document.RemoteCommands)
        {
            catalog.RegisterRemoteCommand(new GemRemoteCommandDefinition(
                item.Name,
                item.Parameters.Select(parameter => new GemRemoteCommandParameterDefinition(
                    parameter.Name,
                    parameter.Format,
                    parameter.Required)).ToArray(),
                item.AllowedControlStates is null
                    ? null
                    : new HashSet<GemControlState>(item.AllowedControlStates),
                item.AllowAdditionalParameters,
                ToCeid(item.AcceptedEventId),
                ToCeid(item.CompletedEventId),
                ToCeid(item.FailedEventId),
                item.HandlerKey));
        }
    }

    private static GemCeid? ToCeid(uint? value) => value is uint id ? new GemCeid(id) : null;
}

public static class GemInterfaceProfileLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static async Task<GemJsonInterfaceProfile> LoadJsonAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using FileStream stream = File.OpenRead(path);
        return await LoadJsonAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<GemJsonInterfaceProfile> LoadJsonAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        byte[] content = buffer.ToArray();
        GemJsonInterfaceProfileDocument document = JsonSerializer.Deserialize<GemJsonInterfaceProfileDocument>(
            content,
            SerializerOptions) ?? throw new InvalidDataException("The GEM interface profile is empty.");

        ValidateDocument(document);
        string hash = Convert.ToHexString(SHA256.HashData(content));
        var profile = new GemJsonInterfaceProfile(document, hash);

        // 先在临时目录中完成引用关系校验，避免将无效 Profile 部分写入正式 Catalog。
        var verificationCatalog = new GemInterfaceCatalog();
        profile.ApplyTo(verificationCatalog);
        verificationCatalog.ValidateAndSeal();
        return profile;
    }

    private static void ValidateDocument(GemJsonInterfaceProfileDocument document)
    {
        if (document.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported GEM profile schemaVersion '{document.SchemaVersion}'.");
        ArgumentException.ThrowIfNullOrWhiteSpace(document.ProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.InterfaceVersion);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
