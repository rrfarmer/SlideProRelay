namespace SlideProRelay.Server.ProPresenter;

/// <summary>
/// Tracks which slides (keyed by ProPresenter's slide uuid) have been shown, so
/// callers can tell when a slide is being repeated. Best-effort and in-memory —
/// it resets when the relay restarts. Lays groundwork for future features that
/// react to repeated slides (e.g. choruses shown multiple times).
/// </summary>
public sealed class SlideHistory
{
    private readonly Dictionary<string, int> _counts = [];
    private readonly Lock _lock = new();
    private string? _lastUuid;

    /// <summary>
    /// Records that <paramref name="uuid"/> is now the live slide. A new
    /// appearance is counted only when the live slide actually changes — not on
    /// every poll of the same slide.
    /// </summary>
    public void Observe(string? uuid)
    {
        if (string.IsNullOrEmpty(uuid))
            return;

        lock (_lock)
        {
            if (uuid == _lastUuid)
                return;

            _lastUuid = uuid;
            _counts[uuid] = _counts.TryGetValue(uuid, out var n) ? n + 1 : 1;
        }
    }

    /// <summary>How many times this slide has been shown so far (0 if never).</summary>
    public int SeenCount(string? uuid)
    {
        if (string.IsNullOrEmpty(uuid))
            return 0;

        lock (_lock)
            return _counts.TryGetValue(uuid, out var n) ? n : 0;
    }
}
