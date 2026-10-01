using System.Text.Json;
using System.Text.Json.Nodes;

namespace CvApi.Tenants;

public static class TenantFiles
{
    private static readonly JsonDocumentOptions ReadOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    /// <summary>
    /// Changes tenant.json programmatically (account settings, own domain). Comments in the file are not kept;
    /// the admin UI's editors keep them because they edit the text client-side.
    /// </summary>
    public static async Task PatchTenantJsonAsync(Tenant tenant, Action<JsonObject> change, CancellationToken ct)
    {
        var file = Path.Combine(tenant.Directory, "tenant.json");
        await WriteLock.WaitAsync(ct);
        try
        {
            var json = JsonNode.Parse(await File.ReadAllTextAsync(file, ct), documentOptions: ReadOptions) as JsonObject ?? [];
            change(json);
            await File.WriteAllTextAsync(file, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
        }
        finally
        {
            WriteLock.Release();
        }
    }
}
