using KwyTemplate.Security.SuperDog;
using Xunit;

namespace KwyTemplate.Tests.Security;

public sealed class SuperDogSecurityKeyCheckerTests
{
    [Fact]
    public void Check_WhenVendorCodeEmpty_FailsBeforeNativeDogCall()
    {
        var checker = new SuperDogSecurityKeyChecker(new SuperDogOptions
        {
            VendorCode = "",
            FeatureId = 1
        });

        SuperDogCheckResult result = checker.Check();

        Assert.False(result.IsSuccess);
        Assert.Equal("VendorCodeEmpty", result.Status);
        Assert.Equal(1, result.FeatureId);
    }

    [Fact]
    public void Check_WhenFeatureIdInvalid_FailsBeforeNativeDogCall()
    {
        var checker = new SuperDogSecurityKeyChecker(new SuperDogOptions
        {
            VendorCode = "vendor",
            FeatureId = 0
        });

        SuperDogCheckResult result = checker.Check();

        Assert.False(result.IsSuccess);
        Assert.Equal("FeatureIdInvalid", result.Status);
        Assert.Equal(0, result.FeatureId);
    }
}
