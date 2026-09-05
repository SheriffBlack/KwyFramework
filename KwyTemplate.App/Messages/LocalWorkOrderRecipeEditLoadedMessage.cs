using KwyTemplate.App.Models;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.App.Messages;

/// <summary>
/// 将本地工单配方加载到 SetView 的运行时编辑模型。
/// 不修改 HomeView 工单，也不写入物理仪表。
/// </summary>
public sealed record LocalWorkOrderRecipeEditLoadedMessage(
    LocalWorkOrderRecipe Recipe,
    MesWorkOrderSetup Setup,
    string FilePath);
