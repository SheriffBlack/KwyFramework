using System.Collections.Concurrent;

namespace KwyPecvd.Device;

public sealed class HardwareComponentRegistry : IHardwareComponentRegistry
{
    private readonly ConcurrentDictionary<
        string,
        IHardwareComponent> components =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IHardwareComponent>
        Components =>
        components.Values.ToArray();

    public void Add(IHardwareComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            component.Id);

        if (components.TryAdd(
                component.Id,
                component))
        {
            return;
        }

        if (components.TryGetValue(
                component.Id,
                out var existing) &&
            ReferenceEquals(existing, component))
        {
            return;
        }

        throw new InvalidOperationException(
            $"A hardware component with id " +
            $"'{component.Id}' is already registered.");
    }

    public bool TryGet<TComponent>(
        string id,
        out TComponent component)
        where TComponent : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (components.TryGetValue(id, out var found) &&
            found is TComponent typed)
        {
            component = typed;
            return true;
        }

        component = default!;
        return false;
    }

    public TComponent GetRequired<TComponent>(
        string id)
        where TComponent : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!components.TryGetValue(id, out var found))
        {
            throw new KeyNotFoundException(
                $"Hardware component not found: {id}");
        }

        return found as TComponent
            ?? throw new InvalidOperationException(
                $"Hardware component '{id}' is " +
                $"{found.GetType().FullName}, not " +
                $"{typeof(TComponent).FullName}.");
    }

    public IReadOnlyCollection<TComponent>
        GetAll<TComponent>()
        where TComponent : class
    {
        return components.Values
            .OfType<TComponent>()
            .ToArray();
    }
}