using KwyTemplate.App.Models;
using KwyTemplate.MES.Abstract.Models;
using Xunit;

namespace KwyTemplate.Tests.App;

public sealed class BraidOptionsTests
{
    [Fact]
    public void FromTapeSetup_WhenNull_ReturnsZeroDefaults()
    {
        BraidOptions options = BraidOptions.FromTapeSetup(null);

        Assert.Equal(0, options.BeforeSpaceQty);
        Assert.Equal(0, options.PackageQty);
        Assert.Equal(0, options.AfterSpaceQty);
        Assert.Equal(0, options.SampleQty);
        Assert.Equal(0, options.BlankQty);
        Assert.Equal(0, options.BackNoFilmQty);
    }

    [Fact]
    public void FromTapeSetup_MapsNullableValuesToEditableOptions()
    {
        var setup = new MesWorkOrderTapeSetup(1, 2, 3, 4, 5, 6);

        BraidOptions options = BraidOptions.FromTapeSetup(setup);

        Assert.Equal(1, options.BeforeSpaceQty);
        Assert.Equal(2, options.PackageQty);
        Assert.Equal(3, options.AfterSpaceQty);
        Assert.Equal(4, options.SampleQty);
        Assert.Equal(5, options.BlankQty);
        Assert.Equal(6, options.BackNoFilmQty);
    }

    [Fact]
    public void ToTapeSetup_UsesCurrentEditableValues()
    {
        var options = new BraidOptions
        {
            BeforeSpaceQty = 10,
            PackageQty = 20,
            AfterSpaceQty = 30,
            SampleQty = 40,
            BlankQty = 50,
            BackNoFilmQty = 60
        };

        MesWorkOrderTapeSetup setup = options.ToTapeSetup();

        Assert.Equal(10, setup.BeforeSpaceQty);
        Assert.Equal(20, setup.PackageQty);
        Assert.Equal(30, setup.AfterSpaceQty);
        Assert.Equal(40, setup.SampleQty);
        Assert.Equal(50, setup.BlankQty);
        Assert.Equal(60, setup.BackNoFilmQty);
    }
}
