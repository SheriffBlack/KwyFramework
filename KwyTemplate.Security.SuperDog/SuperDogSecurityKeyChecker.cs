using KwyTemplate.Security.Licensing;
using SuperDog;

namespace KwyTemplate.Security.SuperDog;

public sealed class SuperDogSecurityKeyChecker : ISecurityKeyChecker
{
    private readonly SuperDogOptions options;

    public SuperDogSecurityKeyChecker(SuperDogOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsPresent() => Check().IsSuccess;

    public SuperDogCheckResult Check()
    {
        if (string.IsNullOrWhiteSpace(options.VendorCode))
        {
            return SuperDogCheckResult.Failed(options.FeatureId, "VendorCodeEmpty", "密码狗 VendorCode 未配置。");
        }

        if (options.FeatureId <= 0)
        {
            return SuperDogCheckResult.Failed(options.FeatureId, "FeatureIdInvalid", "密码狗 FeatureId 必须大于 0。");
        }

        try
        {
            using var dog = new Dog(new DogFeature(DogFeature.FromFeature(options.FeatureId).Feature));
            DogStatus status = dog.Login(options.VendorCode, options.Scope);
            return status == DogStatus.StatusOk
                ? SuperDogCheckResult.Success(options.FeatureId, status.ToString())
                : SuperDogCheckResult.Failed(options.FeatureId, status.ToString(), $"密码狗授权检测失败：{status}");
        }
        catch (DllNotFoundException ex)
        {
            return SuperDogCheckResult.Failed(options.FeatureId, "DllNotFound", ex.Message);
        }
        catch (BadImageFormatException ex)
        {
            return SuperDogCheckResult.Failed(options.FeatureId, "BadImageFormat", ex.Message);
        }
        catch (Exception ex)
        {
            return SuperDogCheckResult.Failed(options.FeatureId, ex.GetType().Name, ex.Message);
        }
    }
}
