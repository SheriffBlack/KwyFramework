namespace Kwy.UI;

/// <summary>
/// Categorised resource keys for the icons included with Kwy UI.
/// The values can be used wherever an icon resource key is accepted.
/// </summary>
public static class KwyIconSets
{
    /// <summary>General-purpose application and navigation icons.</summary>
    public static class General
    {
        public const string Home = IconNames.IconHome;
        public const string Settings = IconNames.IconSet;
        public const string Search = IconNames.IconSearch;
        public const string Save = IconNames.IconSave;
        public const string Folder = IconNames.IconFolder;
        public const string Info = IconNames.IconInfo;
        public const string Success = IconNames.IconSuccess;
        public const string Warning = IconNames.IconWarning;
        public const string Error = IconNames.IconError;
    }

    /// <summary>Icons describing industrial devices, stations, and operations.</summary>
    public static class Industrial
    {
        public const string Plc = IconNames.IconPLC;
        public const string Cpu = IconNames.IconCPU;
        public const string Ram = IconNames.IconRAM;
        public const string Pci = IconNames.IconPCI;
        public const string Station = IconNames.IconStation;
        public const string Measurement = IconNames.IconMeasurement;
        public const string Inching = IconNames.IconInching;
        public const string Processing = IconNames.IconProcessing;
        public const string Flow = IconNames.IconFlow;
    }

    /// <summary>Icons for language selection and Chinese-language variants.</summary>
    public static class Language
    {
        public const string LanguageSelector = IconNames.IconLanguage;
        public const string English = IconNames.IconEnglish;
        public const string SimplifiedChinese = IconNames.IconSimplified;
        public const string TraditionalChinese = IconNames.IconTraditional;
    }
}
