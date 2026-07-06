namespace Kwy.MVVM.Core;

/// <summary>
/// 参数容器接口，提供强类型的参数存取机制。
/// 模仿 Prism 9.0 中的 IParameters，统一 Dialog 和 Navigation 参数。
/// </summary>
public interface IParameters : IEnumerable<KeyValuePair<string, object?>>
{
    void Add(string key, object? value);

    bool ContainsKey(string key);

    int Count { get; }

    T? GetValue<T>(string key);

    IEnumerable<T> GetValues<T>(string key);

    bool TryGetValue<T>(string key, out T value);
}
