namespace Kwy.Communicate.Abstractions;

public static class ProtocolConfigExtensions
{
    public static ConfigValidationResult ValidateDetailed(this IProtocolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Validate()
            ? ConfigValidationResult.Success
            : new ConfigValidationResult(new[] { $"{config.GetType().Name} configuration is invalid." });
    }

    public static void ValidateAndThrow(this IProtocolConfig config)
    {
        var result = config.ValidateDetailed();
        if (!result.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
    }
}
