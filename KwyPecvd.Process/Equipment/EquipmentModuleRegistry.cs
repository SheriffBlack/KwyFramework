using System.Collections.Concurrent;

namespace KwyPecvd.Process.Equipment;

public sealed class EquipmentModuleRegistry : IEquipmentModuleRegistry
{
    private readonly ConcurrentDictionary<string, IEquipmentModuleController> modules = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IEquipmentModuleController> Modules => modules.Values.ToArray();

    public void Add(IEquipmentModuleController module)
    {
        ArgumentNullException.ThrowIfNull(module);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            module.Id);

        if (modules.TryAdd(module.Id, module))
        {
            return;
        }

        // 判断是同一个对象，因此允许幂等注册；一个物理模块ID只能对应一个运行时Controller实例
        if (modules.TryGetValue(module.Id, out var existing) && ReferenceEquals(existing, module))
        {
            return;
        }

        throw new InvalidOperationException(
            $"An equipment module with id " +
            $"'{module.Id}' is already registered.");
    }

    public bool TryGet<TModule>(string id, out TModule module)
        where TModule : class, IEquipmentModuleController
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (modules.TryGetValue(
                id,
                out var found) &&
            found is TModule typed)
        {
            module = typed;
            return true;
        }

        module = default!;
        return false;
    }

    public TModule GetRequired<TModule>(string id)
        where TModule : class, IEquipmentModuleController
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!modules.TryGetValue(
                id,
                out var found))
        {
            throw new KeyNotFoundException(
                $"Equipment module not found: {id}");
        }

        return found as TModule
            ?? throw new InvalidOperationException(
                $"Equipment module '{id}' is " +
                $"{found.GetType().FullName}, not " +
                $"{typeof(TModule).FullName}.");
    }

    public IReadOnlyCollection<TModule> GetAll<TModule>()
        where TModule : class, IEquipmentModuleController
    {
        return modules.Values
            .OfType<TModule>()
            .ToArray();
    }
}