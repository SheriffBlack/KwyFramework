using global::Kwy.UI.WPF.Controls;
using global::Kwy.UI.WPF.Controls.NumberInput;
using global::Kwy.UI.WPF.Controls.Helpers;
using global::Kwy.UI.WPF.Behaviors;
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
    public void KwyWindow_ExposesGenericTemplateIconContract()
    {
        PropertyInfo? iconProperty = typeof(KwyWindow).GetProperty(
            nameof(KwyWindow.Icon),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotNull(iconProperty);
        Assert.Equal(typeof(object), iconProperty.PropertyType);
        Assert.Equal(typeof(KwyWindow), iconProperty.DeclaringType);
    }

    [Fact]
    public void PublicXamlNamespace_IncludesNumberInput()
    {
        IEnumerable<XmlnsDefinitionAttribute> definitions = typeof(KwyWindow).Assembly
            .GetCustomAttributes<XmlnsDefinitionAttribute>();

        Assert.Contains(definitions, definition =>
            definition.XmlNamespace == "http://schemas.kwy.com/ui"
            && definition.ClrNamespace == "Kwy.UI.WPF.Controls.NumberInput");

        Assert.Contains(definitions, definition =>
            definition.XmlNamespace == "http://schemas.kwy.com/ui"
            && definition.ClrNamespace == "Kwy.UI.WPF.Input.Keyboard");
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

    [Fact]
    public void KwyWindow_UsesGenericRightContentSlot()
    {
        Assert.NotNull(typeof(KwyWindow).GetProperty(nameof(KwyWindow.TitleBarRightContent)));
        Assert.Null(typeof(KwyWindow).GetProperty("ShowUserButton"));
        Assert.Null(typeof(KwyWindow).GetProperty("UserCommand"));
    }

    [Fact]
    public void Percent_DoesNotExposeTemplateFormattedText()
    {
        Assert.Equal(typeof(double), KwyPercent.PercentageProperty.PropertyType);
        Assert.Null(typeof(KwyPercent).GetProperty("PercentageText"));
        Assert.Null(typeof(KwyPercent).GetProperty("CurrentCountText"));
        Assert.Null(typeof(KwyPercent).GetProperty("ComputedTooltipText"));
    }

    [Fact]
    public void ResourceStyleKeys_AcceptAnyWpfResourceKey()
    {
        Assert.Equal(typeof(object), ComboBoxHelper.StyleKeyProperty.PropertyType);
        Assert.Equal(typeof(object), ListBoxHelper.StyleKeyProperty.PropertyType);
        Assert.Equal(typeof(object), ToggleSwitchHelper.StyleKeyProperty.PropertyType);
        Assert.Equal(typeof(object), DataGridColumnsHelper.DefaultElementStyleKeyProperty.PropertyType);
        Assert.Equal(typeof(object), FileDropBehavior.DropEffectStyleKeyProperty.PropertyType);
    }

    [Fact]
    public void BindingPaths_RemainStronglyTypedAsStrings()
    {
        Assert.Equal(typeof(string), ComboBoxHelper.IconMemberPathProperty.PropertyType);
        Assert.Equal(typeof(string), DataGridColumnsHelper.RowHeaderBindingPathProperty.PropertyType);
    }

    [Fact]
    public void ToastHost_DoesNotOwnGlobalRegistrationState()
    {
        Assert.Null(typeof(KwyToastHost).GetProperty("Token"));
        Assert.Null(typeof(KwyToastHost).GetEvent("Registered"));
        Assert.Null(typeof(KwyToastHost).GetEvent("Unregistered"));
        Assert.Null(typeof(KwyToastHost).GetMethod("GetRegisteredHosts"));
    }
}
