using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Kwy.UI.WPF.Components.PropertyGrid;

/// <summary>
/// 解析并缓存动态选项源提供程序的调用器。
/// 仅缓存调用方式，不缓存提供程序的动态返回结果。
/// </summary>
internal static class ItemsSourceProviderAccessorCache
{
    private static readonly ConcurrentDictionary<
        ProviderKey,
        Lazy<Func<object, object?>>> Accessors = new();

    public static Func<object, object?> GetAccessor(
        Type sourceType,
        string providerName)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var key = new ProviderKey(sourceType, providerName);
        return Accessors.GetOrAdd(
                key,
                static item => new Lazy<Func<object, object?>>(
                    () => CreateAccessor(item),
                    LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;
    }

    private static Func<object, object?> CreateAccessor(ProviderKey key)
    {
        PropertyInfo? providerProperty = key.SourceType.GetProperty(
            key.ProviderName,
            BindingFlags.Instance | BindingFlags.Public);

        if (providerProperty is { CanRead: true }
            && providerProperty.GetIndexParameters().Length == 0)
        {
            return PropertyAccessorCache.GetGetter(providerProperty);
        }

        MethodInfo? providerMethod = key.SourceType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method =>
                string.Equals(
                    method.Name,
                    key.ProviderName,
                    StringComparison.Ordinal)
                && !method.IsGenericMethodDefinition
                && method.GetParameters().Length == 0
                && method.ReturnType != typeof(void));

        if (providerMethod == null)
        {
            throw new InvalidOperationException(
                $"Cannot find readable property or parameterless, non-void method " +
                $"items source provider '{key.ProviderName}' on {key.SourceType.FullName}.");
        }

        var instance = Expression.Parameter(typeof(object), "instance");
        var typedInstance = Expression.Convert(instance, key.SourceType);
        var call = Expression.Call(typedInstance, providerMethod);
        var boxedResult = Expression.Convert(call, typeof(object));

        return Expression
            .Lambda<Func<object, object?>>(boxedResult, instance)
            .Compile();
    }

    private readonly record struct ProviderKey(
        Type SourceType,
        string ProviderName);
}

