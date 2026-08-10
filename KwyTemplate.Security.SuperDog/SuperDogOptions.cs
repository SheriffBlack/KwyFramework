namespace KwyTemplate.Security.SuperDog;

public sealed class SuperDogOptions
{
    public const int DefaultFeatureId = 1;
    public const string DefaultScope = "<dogscope />";

    public string VendorCode { get; set; } = SuperDogVendorCode.Code;

    public int FeatureId { get; set; } = DefaultFeatureId;

    public string Scope { get; set; } = DefaultScope;
}
