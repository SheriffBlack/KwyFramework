using KwyTemplate.App.Models;

namespace KwyTemplate.App.Messages;

/// <summary>
/// 本地工单已完成运行时下发；设置页可据此切换至首个工位继续编辑。
/// </summary>
public sealed record LocalWorkOrderRecipeAppliedMessage(LocalWorkOrderRecipe Recipe);
