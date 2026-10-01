using System.Net.Http.Json;

namespace CvApi.Accounts;

public interface IEmailSender
{
    bool Enabled { get; }
    Task<bool> SendAsync(string to, string subject, string text, string html, CancellationToken ct);
}

/// <summary>
/// Sends transactional e-mail through the Resend HTTP API (no SMTP server needed). Without an API key, e-mail is
/// disabled; with <c>Email:LogLinks</c> (development only) the message is written to the log instead.
/// </summary>
public sealed class EmailSender(IHttpClientFactory http, IConfiguration config, ILogger<EmailSender> logger) : IEmailSender
{
    public const string HttpClientName = "email";

    private string? ApiKey => config["Email:ResendApiKey"] is { Length: > 0 } k ? k : null;
    private bool LogOnly => config.GetValue("Email:LogLinks", false);

    public bool Enabled => ApiKey is not null || LogOnly;

    public async Task<bool> SendAsync(string to, string subject, string text, string html, CancellationToken ct)
    {
        if (ApiKey is null)
        {
            if (!LogOnly) return false;
            logger.LogWarning("E-mail (Email:LogLinks, not sent) to {To}: {Subject}\n{Text}", to, subject, text);
            return true;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new
            {
                from = config["Email:From"] is { Length: > 0 } from ? from : "CV <noreply@example.com>",
                to = new[] { to },
                subject,
                text,
                html,
            }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
        try
        {
            using var response = await http.CreateClient(HttpClientName).SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return true;
            logger.LogError("E-mail to {Domain} failed: {Status} {Body}", Domain(to), (int)response.StatusCode,
                await response.Content.ReadAsStringAsync(ct));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "E-mail to {Domain} failed", Domain(to));
        }
        return false;
    }

    // Logs name only the domain of an address.
    private static string Domain(string email) => email[(email.IndexOf('@') + 1)..];
}
