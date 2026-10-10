using Kwy.ComponentModel.Metadata;
using KwyPecvd.Contracts.App.Localization;

namespace KwyPecvd.Shell.Models;

public class LanguageModel
{
    public string? Icon { get; set; }

    public string Language => PropertyMetadataReader.GetEnumDescription(LanguageType);

    public LanguageType LanguageType { get; set; }

    public override string ToString() => Language;
}
