namespace KwyTemplate.App.Messages;

/// <summary>请求参数字典加载当前选中的本地工单配方。</summary>
/// <remarks>
/// SetView 顶部“应用”发布此消息；参数字典据此读取选中的 JSON，
/// 再通过编辑加载消息刷新工位参数，不修改 HomeView 工单。
/// </remarks>
public sealed record LocalWorkOrderRecipeLoadRequestedMessage;
