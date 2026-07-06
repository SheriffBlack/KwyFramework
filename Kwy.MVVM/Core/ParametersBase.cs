using System.Collections;

namespace Kwy.MVVM.Core;

/// <summary>
/// 参数容器的基础实现。
/// 已针对 .NET 8 进行了极致性能优化，使用 ReadOnlySpan<char> 消除字符串解析时的数组内存分配。
/// </summary>
public abstract class ParametersBase : IParameters
{
    // 【性能优化 2】：延迟初始化列表。很多导航不需要参数，我们在此实现了真正的零堆积分配
    private List<KeyValuePair<string, object?>>? _entries;

    private List<KeyValuePair<string, object?>> Entries => _entries ??= new List<KeyValuePair<string, object?>>();

    protected ParametersBase()
    { }

    protected ParametersBase(string query)
    {
        if (string.IsNullOrEmpty(query)) return;

        // 【性能核心】：将原始字符串转换为只读切片，开始栈上零分配游走
        ReadOnlySpan<char> querySpan = query.AsSpan();

        while (!querySpan.IsEmpty)
        {
            // 查找 '&' 分隔符
            int ampersandIndex = querySpan.IndexOf('&');
            ReadOnlySpan<char> pairSpan;

            if (ampersandIndex == -1)
            {
                // 没找到 '&'，说明这是最后一组参数
                pairSpan = querySpan;
                querySpan = ReadOnlySpan<char>.Empty;
            }
            else
            {
                // 截取 '&' 之前的内容作为当前键值对
                pairSpan = querySpan.Slice(0, ampersandIndex);
                // 将游标移动到 '&' 之后，准备下一次循环
                querySpan = querySpan.Slice(ampersandIndex + 1);
            }

            // 忽略连续的 "&&" 导致的空切片
            if (pairSpan.IsEmpty) continue;

            // 在当前键值对中查找 '='
            int equalsIndex = pairSpan.IndexOf('=');

            if (equalsIndex == -1)
            {
                // 没有 '='，说明只有键，没有值
                Add(pairSpan.ToString(), null);
            }
            else
            {
                // 切割出键和值
                ReadOnlySpan<char> keySpan = pairSpan.Slice(0, equalsIndex);
                ReadOnlySpan<char> valueSpan = pairSpan.Slice(equalsIndex + 1);

                // 注意：由于 Dictionary 需要 string 作为键，这里最终还是会分配最终的 string 实例。
                // 但我们完美避开了所有中间的 string[] 数组分配和 pair 临时字符串的分配！
                var key = keySpan.ToString();
                var value = Uri.UnescapeDataString(valueSpan.ToString());

                Add(key, value);
            }
        }
    }

    public void Add(string key, object? value)
    {
        Entries.Add(new KeyValuePair<string, object?>(key, value));
    }

    // 【性能优化】：抛弃 LINQ 的 .Any()，避免闭包和枚举器分配
    public bool ContainsKey(string key)
    {
        if (_entries == null) return false;
        foreach (var entry in _entries)
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public int Count => _entries?.Count ?? 0;

    // 【性能优化】：抛弃 LINQ 的 .FirstOrDefault()
    public T? GetValue<T>(string key)
    {
        if (_entries == null) return default;
        foreach (var entry in _entries)
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal))
            {
                if (entry.Value is T typedValue) return typedValue;
                return default;
            }
        }
        return default;
    }

    // 这里保留 LINQ 也可以，因为 GetValues 通常用于获取集合，不可避免要分配内存。
    // 但为了极致统一，也可以用 yield return 重写
    public IEnumerable<T> GetValues<T>(string key)
    {
        if (_entries == null) yield break;
        foreach (var entry in _entries)
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal) && entry.Value is T typedValue)
            {
                yield return typedValue;
            }
        }
    }

    // 【性能优化】：抛弃 LINQ
    public bool TryGetValue<T>(string key, out T value)
    {
        value = default!;
        if (_entries == null) return false;

        foreach (var entry in _entries)
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal))
            {
                if (entry.Value is T typedValue)
                {
                    value = typedValue;
                    return true;
                }
                break;
            }
        }
        return false;
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => Entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString()
    {
        if (_entries == null || _entries.Count == 0) return string.Empty;
        var query = string.Join("&", _entries.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value?.ToString() ?? string.Empty)}"));
        return query;
    }
}
