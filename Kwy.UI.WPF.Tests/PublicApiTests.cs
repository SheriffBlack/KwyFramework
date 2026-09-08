using global::Kwy.UI.WPF.Controls;
using global::Kwy.UI.WPF.Controls.NumberInput;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;

namespace Kwy.UI.WPF.Tests;

public sealed class PublicApiTests
{
    [Fact]
    public void LegendHeader_DependencyProperty_AcceptsAnyObject()
    {
        Assert.Equal(typeof(object), KwyLegend.HeaderProperty.PropertyType);
    }

    [Fact]
    public void KwyWindow_UsesFrameworkIconContract()
    {
        PropertyInfo? iconProperty = typeof(KwyWindow).GetProperty(nameof(Window.Icon));

        Assert.NotNull(iconProperty);
        Assert.Equal(typeof(ImageSource), iconProperty.PropertyType);
        Assert.Equal(typeof(Window), iconProperty.DeclaringType);
    }

    [Fact]
    public void PublicXamlNamespace_IncludesNumberInput()
    {
        IEnumerable<XmlnsDefinitionAttribute> definitions = typeof(KwyWindow).Assembly
            .GetCustomAttributes<XmlnsDefinitionAttribute>();

        Assert.Contains(definitions, definition =>
            definition.XmlNamespace == "http://schemas.kwy.com/ui"
            && definition.ClrNamespace == "Kwy.UI.WPF.Controls.NumberInput");
    }

    [Fact]
    public void NumericBaseConverter_UsesExplicitBaseWithoutSharedState()
    {
        var hexadecimal = new NumericBaseConverter
        {
            Base = NumericBase.Hexadecimal,
            MinimumDigits = 2
        };
        var decimalConverter = new NumericBaseConverter();

        Assert.Equal("0x10", hexadecimal.Convert(16, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("16", decimalConverter.Convert(16, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PublicConverters_DoNotExposeBusinessJudgementConverter()
    {
        Type? converter = typeof(KwyWindow).Assembly.GetType("Kwy.UI.WPF.Converters.JudgeToBrushConverter");

        Assert.Null(converter);
    }
}
