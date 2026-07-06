using System.ComponentModel;

namespace KwyTemplate.Contracts.Localization;

public enum LanguageType
{
    [Description("简体")]
    ZH_CN,

    [Description("繁體")]
    ZH_TW,

    [Description("English")]
    EN_US,
}
