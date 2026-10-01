using System.Net;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Accounts;

/// <summary>
/// E-mails to CV owners with an account (docs/REQUIREMENTS_SAAS.md §9): an invite was opened for the first time, and
/// the Pro pass ends in 3 days. Sending happens in the background so visitors never wait for it.
/// </summary>
public sealed class OwnerNotifier(IServiceScopeFactory scopes, TenantOwners owners, IConfiguration config, TimeProvider time,
    ILogger<OwnerNotifier> logger) : BackgroundService
{
    private static readonly TimeSpan ReminderBefore = TimeSpan.FromDays(3);
    private readonly Channel<(string TenantId, string Label, bool ViaQr)> _opened =
        Channel.CreateBounded<(string, string, bool)>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    /// <summary>Called on the first redemption of an invite; only tenants with an owner account are notified.</summary>
    public void InviteOpened(string tenantId, string label, bool viaQr)
    {
        if (owners.Get(tenantId) is null) return;
        _opened.Writer.TryWrite((tenantId, label, viaQr));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reminders = RemindersLoopAsync(stoppingToken);
        await foreach (var (tenantId, label, viaQr) in _opened.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await SendOpenedAsync(tenantId, label, viaQr, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Invite notification for tenant {Tenant} failed", tenantId);
            }
        }
        await reminders;
    }

    private string DashboardUrl => $"{config["Cv:SharedBaseUrl"]?.TrimEnd('/')}/admin";

    public async Task SendOpenedAsync(string tenantId, string label, bool viaQr, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        if (!email.Enabled) return;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.TenantId == tenantId, ct);
        if (user is null || user.BlockedAt is not null || user.NotifyOnOpen == false) return;

        var name = string.IsNullOrWhiteSpace(label) ? "An invite without label" : $"Your invite \"{label}\"";
        var how = viaQr ? " via the QR code of the printed PDF" : "";
        var text = $"{name} was just opened for the first time{how}.\n\nDashboard: {DashboardUrl}\n\n" +
                   "You can turn these e-mails off in the dashboard (Account).";
        var html = $"<p>{WebUtility.HtmlEncode(name)} was just opened for the first time{WebUtility.HtmlEncode(how)}.</p>" +
                   $"<p><a href=\"{WebUtility.HtmlEncode(DashboardUrl)}\">Open the dashboard</a></p>" +
                   "<p style=\"color:#666\">You can turn these e-mails off in the dashboard (Account).</p>";
        await email.SendAsync(user.Email, "Your CV was opened", text, html, ct);
    }

    private async Task RemindersLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await SendProRemindersAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Pro reminders failed");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    /// <summary>Once per pass: "Pro ends on …" 3 days before the end (manual "forever" plans never end).</summary>
    public async Task<int> SendProRemindersAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        if (!email.Enabled) return 0;
        var now = time.GetUtcNow();
        var due = (await db.Users.Where(u => u.ProUntil != null && !u.ProForever && u.BlockedAt == null).ToListAsync(ct))
            .Where(u => u.ProUntil > now && u.ProUntil - now <= ReminderBefore && u.ProReminderSentFor != u.ProUntil)
            .ToList();
        foreach (var user in due)
        {
            var until = user.ProUntil!.Value.ToString("yyyy-MM-dd");
            var text = $"Your Pro pass ends on {until}. Afterwards your CV stays online on the free plan; invites beyond the free " +
                       $"limit keep working until they expire, but you cannot create new ones.\n\nExtend Pro: {DashboardUrl}";
            var html = $"<p>Your Pro pass ends on <b>{until}</b>. Afterwards your CV stays online on the free plan; invites beyond the " +
                       "free limit keep working until they expire, but you cannot create new ones.</p>" +
                       $"<p><a href=\"{WebUtility.HtmlEncode(DashboardUrl)}\">Extend Pro</a></p>";
            if (await email.SendAsync(user.Email, "Your Pro pass ends soon", text, html, ct))
                user.ProReminderSentFor = user.ProUntil;
        }
        await db.SaveChangesAsync(ct);
        return due.Count;
    }
}
