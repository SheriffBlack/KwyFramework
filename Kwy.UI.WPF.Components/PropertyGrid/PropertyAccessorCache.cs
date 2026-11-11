using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Kwy.UI.WPF.Components.PropertyGrid;

/// <summary>
/// 缓存反射属性编译后的访问器；访问器不绑定具体对象实例，可供同一属性的不同实例复用。
/// </summary>
internal static class PropertyAccessorCache
{
    private static readonly ConcurrentDictionary<
        PropertyInfo,
        Lazy<PropertyAccessors>> Accessors = new();

    public static Func<object, object?> GetGetter(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return GetAccessors(property).Getter;
    }

    public static Action<object, object?> GetSetter(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return GetAccessors(property).Setter.Value
            ?? throw new ArgumentException(
                $"Property '{property.Name}' is not writable.",
                nameof(property));
    }

    private static PropertyAccessors GetAccessors(PropertyInfo property)
        => Accessors.GetOrAdd(
                property,
                static item => new Lazy<PropertyAccessors>(
                    () => CreateAccessors(item),
                    LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;

    private static PropertyAccessors CreateAccessors(PropertyInfo property)
    {
        if (!property.CanRead || property.GetIndexParameters().Length != 0)
        {
            throw new ArgumentException(
                $"Property '{property.Name}' must be a readable, non-indexed property.",
                nameof(property));
        }

        Type declaringType = property.DeclaringType
            ?? throw new ArgumentException(
                "The property must have a declaring type.",
                nameof(property));

        var instance = Expression.Parameter(typeof(object), "instance");
        var typedInstance = Expression.Convert(instance, declaringType);
        var propertyAccess = Expression.Property(typedInstance, property);
        var boxedValue = Expression.Convert(propertyAccess, typeof(object));

        var getter = Expression
            .Lambda<Func<object, object?>>(boxedValue, instance)
            .Compile();

        var setter = new Lazy<Action<object, object?>?>(
            () => CreateSetter(property),
            LazyThreadSafetyMode.ExecutionAndPublication);

        return new PropertyAccessors(getter, setter);
    }

    private static Action<object, object?>? CreateSetter(PropertyInfo property)
    {
        if (!property.CanWrite)
        {
            return null;
        }

        Type declaringType = property.DeclaringType
            ?? throw new ArgumentException(
                "The property must have a declaring type.",
                nameof(property));

        var instance = Expression.Parameter(typeof(object), "instance");
        var value = Expression.Parameter(typeof(object), "value");
        var typedInstance = Expression.Convert(instance, declaringType);
        var typedValue = Expression.Convert(value, property.PropertyType);
        var propertyAccess = Expression.Property(typedInstance, property);
        var assignment = Expression.Assign(propertyAccess, typedValue);

        return Expression
            .Lambda<Action<object, object?>>(assignment, instance, value)
            .Compile();
    }

    private sealed record PropertyAccessors(
        Func<object, object?> Getter,
        Lazy<Action<object, object?>?> Setter);
}

