using System.Collections.Concurrent;
using CvApi.Tenants;
using CvApi.Versioning;

namespace CvApi.Tracking;

/// <summary>
/// Git SHA of the CV content a visitor gets (§6.4, R6.14): the pinned revision, or the tenant's current revision
/// registered by tools/cv-sync.sh. "&lt;sha&gt;-dirty" when the live files differ from that commit, "unversioned"
/// when no revision was registered. Cached briefly because it hashes the tenant's files.
/// </summary>
public sealed class CvSourceVersion(TimeProvider time)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, string Value)> _cache = new();

    public string For(Tenant tenant, string? pinnedRevision)
    {
        // A pinned revision is served from its own snapshot, which is always clean.
        if (pinnedRevision is not null && RevisionStore.Resolve(tenant, pinnedRevision) is { } pinned) return pinned;

        var now = time.GetUtcNow();
        if (_cache.TryGetValue(tenant.Id, out var cached) && now - cached.At < CacheDuration) return cached.Value;

        var index = RevisionStore.Read(tenant);
        var current = index.Revisions.FirstOrDefault(r => r.Sha == index.Current);
        var value = current is null
            ? "unversioned"
            : TenantHashes.Diff(current.Files, TenantHashes.DataFiles(tenant.Directory)).Count > 0 ? $"{current.Sha}-dirty" : current.Sha;
        _cache[tenant.Id] = (now, value);
        return value;
    }
}
