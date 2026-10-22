namespace Kwy.MVVM.Core;

/// <summary>
/// 参数容器接口，提供强类型的参数存取机制。
/// 统一对话框和导航场景的参数访问方式。
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
