using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CvApi.Accounts;

/// <summary>Pro passes sold through Paddle, the merchant of record (docs/REQUIREMENTS_SAAS.md §5).</summary>
public static class BillingEndpoints
{
    public const string SignatureHeader = "Paddle-Signature";
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var billing = app.MapGroup("/billing");

        billing.MapGet("/config", (TenantOwners owners, IConfiguration config) =>
        {
            var enabled = PaddleEnabled(config);
            return Results.Ok(new
            {
                provider = enabled ? "paddle" : null,
                environment = enabled ? (config["Billing:Paddle:Environment"] == "production" ? "production" : "sandbox") : null,
                clientToken = enabled ? config["Billing:Paddle:ClientToken"] : null,
                freeMaxActiveInvites = owners.Plans.FreeMaxActiveInvites,
                passes = owners.Plans.Passes.Select(p => new
                {
                    id = p.Id,
                    days = p.Days,
                    amount = p.Amount,
                    currency = p.Currency,
                    priceId = enabled ? p.PaddlePriceId : null,
                }),
            });
        });

        billing.MapPost("/paddle/webhook", async (HttpRequest request, AccountsDbContext db, TenantOwners owners, IConfiguration config,
            TimeProvider time, ILoggerFactory loggers, CancellationToken ct) =>
        {
            var logger = loggers.CreateLogger("Billing");
            var secret = config["Billing:Paddle:WebhookSecret"];
            if (string.IsNullOrEmpty(secret)) return Results.NotFound();

            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(ct);
            if (!VerifySignature(request.Headers[SignatureHeader].ToString(), body, secret, time.GetUtcNow()))
                return Results.Unauthorized();

            JsonDocument doc;
            try { doc = JsonDocument.Parse(body); }
            catch (JsonException) { return Results.BadRequest(); }
            using (doc)
            {
                var root = doc.RootElement;
                var eventType = root.TryGetProperty("event_type", out var et) ? et.GetString() : null;
                if (!root.TryGetProperty("data", out var data)) return Results.Ok();
                switch (eventType)
                {
                    case "transaction.completed":
                        await ApplyTransactionAsync(data, db, owners, time.GetUtcNow(), logger, ct);
                        break;
                    case "adjustment.created":
                    case "adjustment.updated":
                        await RecordAdjustmentAsync(data, db, time.GetUtcNow(), ct);
                        break;
                }
            }
            return Results.Ok();
        });
    }

    public static bool PaddleEnabled(IConfiguration config) =>
        !string.IsNullOrEmpty(config["Billing:Paddle:ClientToken"]) && !string.IsNullOrEmpty(config["Billing:Paddle:WebhookSecret"]);

    /// <summary>Paddle-Signature: "ts=&lt;unix&gt;;h1=&lt;hex hmac-sha256 of "ts:body"&gt;" (several h1 during secret rotation).</summary>
    public static bool VerifySignature(string header, string body, string secret, DateTimeOffset now)
    {
        string? ts = null;
        var signatures = new List<string>();
        foreach (var part in header.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "ts") ts = kv[1];
            else if (kv[0] == "h1") signatures.Add(kv[1]);
        }
        if (ts is null || signatures.Count == 0 || !long.TryParse(ts, NumberStyles.None, CultureInfo.InvariantCulture, out var unix)) return false;
        if ((now - DateTimeOffset.FromUnixTimeSeconds(unix)).Duration() > MaxClockSkew) return false;

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{ts}:{body}"));
        return signatures.Any(s =>
        {
            try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(s)); }
            catch (FormatException) { return false; }
        });
    }

    /// <summary>Adds the pass's days to the buyer's Pro time; idempotent per transaction id (§5 S5.3).</summary>
    public static async Task ApplyTransactionAsync(JsonElement data, AccountsDbContext db, TenantOwners owners, DateTimeOffset now,
        ILogger logger, CancellationToken ct)
    {
        var transactionId = Str(data, "id");
        if (string.IsNullOrEmpty(transactionId)) return;
        if (await db.Payments.AnyAsync(p => p.Provider == "paddle" && p.ExternalId == transactionId, ct)) return;

        Guid? userId = data.TryGetProperty("custom_data", out var custom) && custom.ValueKind == JsonValueKind.Object
                       && Guid.TryParse(Str(custom, "userId"), out var parsed) ? parsed : null;
        var user = userId is { } id ? await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct) : null;

        // One pass per transaction; quantity > 1 multiplies the days.
        PassOption? pass = null;
        var quantity = 1;
        if (data.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var priceId = item.TryGetProperty("price", out var price) ? Str(price, "id") : Str(item, "price_id");
                pass = owners.Plans.Passes.FirstOrDefault(p => p.PaddlePriceId is not null && p.PaddlePriceId == priceId);
                if (pass is null) continue;
                if (item.TryGetProperty("quantity", out var q) && q.TryGetInt32(out var n) && n is > 0 and <= 10) quantity = n;
                break;
            }
        }

        long amount = 0;
        string? currency = Str(data, "currency_code");
        if (data.TryGetProperty("details", out var details) && details.TryGetProperty("totals", out var totals)
            && long.TryParse(Str(totals, "grand_total") ?? Str(totals, "total"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var total))
            amount = total;

        var matched = user is not null && pass is not null;
        var days = pass is null ? 0 : pass.Days * quantity;
        db.Payments.Add(new Payment
        {
            Provider = "paddle",
            ExternalId = transactionId,
            UserId = user?.Id,
            Email = user?.Email,
            Pass = pass?.Id,
            Days = days,
            Amount = amount,
            Currency = currency,
            Status = matched ? PaymentStatus.Completed : PaymentStatus.Unmatched,
            CreatedAt = now,
        });
        if (matched)
        {
            user!.ProUntil = (user.ProUntil > now ? user.ProUntil.Value : now).AddDays(days);
            logger.LogInformation("Pass {Pass} x{Quantity} for user {User}: Pro until {Until}", pass!.Id, quantity, user.Id, user.ProUntil);
        }
        else
        {
            logger.LogWarning("Unmatched Paddle transaction {Transaction} (user found: {User}, pass found: {Pass})",
                transactionId, user is not null, pass is not null);
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same transaction arrived twice at the same moment; the other request applied it.
            return;
        }
        owners.Invalidate();
    }

    /// <summary>Refunds / chargebacks are recorded; the super-admin adjusts Pro time (§5 S5.4).</summary>
    private static async Task RecordAdjustmentAsync(JsonElement data, AccountsDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var action = Str(data, "action");
        if (action is not ("refund" or "chargeback") || Str(data, "status") != "approved") return;
        var adjustmentId = Str(data, "id");
        if (adjustmentId is null || await db.Payments.AnyAsync(p => p.Provider == "paddle" && p.ExternalId == adjustmentId, ct)) return;
        var transactionId = Str(data, "transaction_id");
        var original = transactionId is null ? null : await db.Payments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Provider == "paddle" && p.ExternalId == transactionId, ct);
        db.Payments.Add(new Payment
        {
            Provider = "paddle",
            ExternalId = adjustmentId,
            UserId = original?.UserId,
            Email = original?.Email,
            Pass = original?.Pass,
            Status = action == "refund" ? PaymentStatus.Refunded : PaymentStatus.Chargeback,
            Note = $"transaction {transactionId}",
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
    }

    private static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v)
            ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => null }
            : null;
}
