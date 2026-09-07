using KwyTemplate.App.Models;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.App.Messages;

/// <summary>参数字典选择本地工单后，请 HomeView 复用统一工单下发链路。</summary>
public sealed record LocalWorkOrderRecipeLoadedMessage(
    LocalWorkOrderRecipe Recipe,
    MesWorkOrderSetup Setup,
    bool PrepareForEditing = false,
    bool ShowImportSuccess = false,
    string? FilePath = null);
