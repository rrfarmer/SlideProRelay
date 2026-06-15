using Microsoft.Extensions.Primitives;

namespace SlideProRelay.Server.Settings;

/// <summary>
/// A configuration source whose values can be updated at runtime.
/// Calling Update() fires the IChangeToken chain so IOptionsMonitor&lt;T&gt;
/// automatically reflects the new values without a server restart.
/// </summary>
internal sealed class MutableConfigSource : IConfigurationSource, IConfigurationProvider
{
    private volatile Dictionary<string, string?> _data =
        new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource _cts = new();

    public void Update(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var next = new Dictionary<string, string?>(_data, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in values)
            next[k] = v;
        _data = next;

        // Fire the change token so IOptionsMonitor<T> picks up new values.
        var prev = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        prev.Cancel();
        prev.Dispose();
    }

    // IConfigurationSource
    public IConfigurationProvider Build(IConfigurationBuilder _) => this;

    // IConfigurationProvider
    public bool TryGet(string key, out string? value) => _data.TryGetValue(key, out value);
    public void Set(string key, string? value) { }
    public IChangeToken GetReloadToken() => new CancellationChangeToken(_cts.Token);
    public void Load() { }

    public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
    {
        var prefix = parentPath is null ? string.Empty : parentPath + ConfigurationPath.KeyDelimiter;
        return _data
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(kv => ConfigurationPath.GetSectionKey(kv.Key[prefix.Length..]))
            .Concat(earlierKeys)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
    }
}
