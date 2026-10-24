namespace Kwy.Communicate.Abstractions;

public static class ProtocolConfigExtensions
{
    public static ConfigValidateResult ValidateDetailed(this IProtocolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Validate()
            ? ConfigValidateResult.Success
            : new ConfigValidateResult(new[] { $"{config.GetType().Name} configuration is invalid." });
    }

    public static void ValidateAndThrow(this IProtocolConfig config)
    {
        var result = config.ValidateDetailed();
        if (!result.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
    }
}
