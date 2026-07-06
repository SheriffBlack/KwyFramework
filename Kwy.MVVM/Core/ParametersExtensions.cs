namespace Kwy.MVVM.Core;

public static class ParametersExtensions
{
    public static TParameters AddValue<TParameters, TValue>(
        this TParameters parameters,
        TValue value,
        string? key = null)
        where TParameters : IParameters
    {
        ArgumentNullException.ThrowIfNull(parameters);

        parameters.Add(key ?? GetDefaultKey<TValue>(), value);
        return parameters;
    }

    public static T? GetValueOrDefault<T>(
        this IParameters parameters,
        string? key = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return parameters.GetValue<T>(key ?? GetDefaultKey<T>());
    }

    public static T GetRequiredValue<T>(
        this IParameters parameters,
        string? key = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        string effectiveKey = key ?? GetDefaultKey<T>();
        if (parameters.TryGetValue<T>(effectiveKey, out var value))
        {
            return value;
        }

        throw new KeyNotFoundException(
            $"参数 '{effectiveKey}' 不存在或无法转换为 {typeof(T).FullName}。");
    }

    private static string GetDefaultKey<T>()
        => typeof(T).FullName ?? typeof(T).Name;
}
