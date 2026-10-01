using System.Text.Json;

namespace CvApi.Access;

/// <summary>Website link clicks counted per printed PDF (<see cref="Invite.LinkClicksJson"/>).</summary>
public static class LinkClicks
{
    public sealed record Entry(string Url, int Count, DateTimeOffset? LastAt);

    public static Dictionary<string, Entry> Parse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, Entry>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
