using System.Collections.Concurrent;
using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using System.Text.RegularExpressions;

namespace CvApi.Tenants;

public sealed record FetchResult(string? Sha, string? Error);

/// <summary>
/// Fetches a CV revision from the tenant's git repository again (e.g. after its snapshot was pruned), using the
/// repo URL and folder that tools/cv-sync.sh reported. Only the requested commit is fetched (shallow) into a
/// bare cache repo under {DataPath}/git/{tenant}; the tenant folder is extracted with `git archive`.
/// Private repos: set Git__Token (HTTPS, sent as basic auth with Git__Username, default "x-access-token").
/// </summary>
public sealed partial class GitRevisionFetcher(IConfiguration configuration, TimeProvider time, ILogger<GitRevisionFetcher> logger)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    /// <summary>A commit SHA, tag or branch name. No leading dash (would be read as an option), no "..".</summary>
    [GeneratedRegex(@"^(?!.*\.\.)[A-Za-z0-9][A-Za-z0-9._/-]{0,199}$")]
    public static partial Regex RefRegex();

    [GeneratedRegex(@"^(?!.*\.\.)[A-Za-z0-9._/-]{0,300}$")]
    private static partial Regex RepoPathRegex();

    [GeneratedRegex(@"^cv\.[a-z]{2}(-[A-Z]{2})?\.json$")]
    private static partial Regex CvFileRegex();

    [GeneratedRegex(@"^assets/[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex AssetFileRegex();

    private string DataPath => Path.GetFullPath(configuration["Cv:DataPath"] ?? "/data");

    /// <summary>Checks a source reported by cv-sync.sh: HTTPS repo (local repos only if Git:AllowLocalRepos) and a relative folder.</summary>
    public bool IsValidSource(RevisionSource source) =>
        RepoPathRegex().IsMatch(source.Path)
        && (source.Repo.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || configuration.GetValue("Git:AllowLocalRepos", false));

    /// <summary>Fetches <paramref name="reference"/> (SHA, tag or branch) and stores its CV as a (non-current) snapshot.</summary>
    public async Task<FetchResult> FetchAsync(Tenant tenant, string reference, CancellationToken ct)
    {
        reference = reference.Trim();
        if (!RefRegex().IsMatch(reference)) return new FetchResult(null, "invalid_ref");
        var source = RevisionStore.Read(tenant).Source;
        if (source is null || !IsValidSource(source)) return new FetchResult(null, "no_git_source");

        var gate = Locks.GetOrAdd(tenant.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        var staging = Path.Combine(DataPath, "git", $"{tenant.Id}.extract-{Guid.NewGuid():N}");
        try
        {
            var repo = Path.Combine(DataPath, "git", tenant.Id);
            if (!Directory.Exists(Path.Combine(repo, "objects")))
            {
                Directory.CreateDirectory(repo);
                await GitAsync(repo, ct, "init", "--bare", "--quiet");
            }

            await GitAsync(repo, ct, "fetch", "--depth=1", "--no-tags", "--quiet", source.Repo, reference);
            var sha = Encoding.UTF8.GetString(await GitAsync(repo, ct, "rev-parse", "FETCH_HEAD^{commit}")).Trim();
            if (!RevisionStore.ShaRegex().IsMatch(sha) || sha.Length != 40) return new FetchResult(null, "fetch_failed");

            if (RevisionStore.Read(tenant).Revisions.Any(r => r.Sha == sha))
            {
                RevisionStore.AddRef(tenant, reference, sha);
                return new FetchResult(sha, null);
            }

            var meta = Encoding.UTF8.GetString(await GitAsync(repo, ct, "log", "-1", "--format=%s%x00%cI", sha)).TrimEnd('\n').Split('\0');
            var prefix = source.Path.Trim('/');
            var archive = await GitAsync(repo, ct, prefix.Length == 0
                ? ["archive", "--format=tar", sha]
                : ["archive", "--format=tar", sha, "--", prefix]);

            Directory.CreateDirectory(staging);
            var count = Extract(archive, prefix.Length == 0 ? "" : prefix + "/", staging);
            if (count == 0) return new FetchResult(null, "no_cv_in_revision");

            DateTimeOffset? committedAt = meta.Length > 1 && DateTimeOffset.TryParse(meta[1], out var at) ? at : null;
            RevisionStore.Store(tenant, sha, staging, meta[0], committedAt, time.GetUtcNow(), makeCurrent: false);
            RevisionStore.AddRef(tenant, reference, sha);
            logger.LogInformation("Fetched CV revision {Sha} ({Ref}) of tenant {Tenant} from git", sha, reference, tenant.Id);
            return new FetchResult(sha, null);
        }
        catch (GitException ex)
        {
            logger.LogWarning("git failed for tenant {Tenant}, ref {Ref}: {Message}", tenant.Id, reference, ex.Message);
            return new FetchResult(null, "fetch_failed: " + ex.Message);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            gate.Release();
        }
    }

    /// <summary>Writes cv.&lt;locale&gt;.json and assets/* below <paramref name="prefix"/>; returns the number of CV files.</summary>
    private static int Extract(byte[] tar, string prefix, string target)
    {
        var cvFiles = 0;
        using var reader = new TarReader(new MemoryStream(tar));
        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.DataStream is null) continue;
            if (!entry.Name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var relative = entry.Name[prefix.Length..];
            var isCv = CvFileRegex().IsMatch(relative);
            if (!isCv && !AssetFileRegex().IsMatch(relative)) continue;

            var path = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var file = File.Create(path)) entry.DataStream.CopyTo(file);
            if (isCv) cvFiles++;
        }
        return cvFiles;
    }

    private async Task<byte[]> GitAsync(string repo, CancellationToken ct, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repo,
        };
        start.ArgumentList.Add("--git-dir=" + repo);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["HOME"] = repo;
        // Credentials go through the environment, never the command line.
        if (configuration["Git:Token"] is { Length: > 0 } token)
        {
            var user = configuration["Git:Username"] is { Length: > 0 } u ? u : "x-access-token";
            start.Environment["GIT_CONFIG_COUNT"] = "1";
            start.Environment["GIT_CONFIG_KEY_0"] = "http.extraHeader";
            start.Environment["GIT_CONFIG_VALUE_0"] = "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{token}"));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.GetValue("Git:TimeoutSeconds", 60)));
        Process process;
        try
        {
            process = Process.Start(start) ?? throw new GitException("git could not be started");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new GitException("git is not installed");
        }
        using (process)
        {
            using var output = new MemoryStream();
            var stdout = process.StandardOutput.BaseStream.CopyToAsync(output, timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(timeout.Token));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                throw new GitException("timeout");
            }
            if (process.ExitCode != 0)
                throw new GitException((await stderr).Trim().Split('\n').LastOrDefault() ?? $"exit {process.ExitCode}");
            return output.ToArray();
        }
    }

    private sealed class GitException(string message) : Exception(message);
}
