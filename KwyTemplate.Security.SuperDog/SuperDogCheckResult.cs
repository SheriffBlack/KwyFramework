namespace KwyTemplate.Security.SuperDog;

public sealed record SuperDogCheckResult(
    bool IsSuccess,
    int FeatureId,
    string Status,
    string? Message = null)
{
    public static SuperDogCheckResult Success(int featureId, string status)
        => new(true, featureId, status);

    public static SuperDogCheckResult Failed(int featureId, string status, string? message = null)
        => new(false, featureId, status, message);
}
