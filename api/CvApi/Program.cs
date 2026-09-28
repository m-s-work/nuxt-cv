using System.Threading.RateLimiting;
using CvApi.Access;
using CvApi.Endpoints;
using CvApi.Pdf;
using CvApi.Tenants;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

// Container health check (the runtime image has no curl): `dotnet CvApi.dll --healthcheck`.
if (args.Contains("--healthcheck"))
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';')[0] ?? "8080";
    try { return (await http.GetAsync($"http://127.0.0.1:{port}/api/health")).IsSuccessStatusCode ? 0 : 1; }
    catch (HttpRequestException) { return 1; }
    catch (TaskCanceledException) { return 1; }
}

var builder = WebApplication.CreateBuilder(args);

var dataPath = Path.GetFullPath(builder.Configuration["Cv:DataPath"] ?? "/data");
Directory.CreateDirectory(dataPath);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TenantStore>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<PdfService>();
builder.Services.AddHttpClient<IPdfRenderer, HttpPdfRenderer>(client =>
{
    // Internal renderer container (compose service "pdf"). Empty = PDF feature disabled.
    var rendererUrl = builder.Configuration["Pdf:RendererUrl"];
    if (!string.IsNullOrEmpty(rendererUrl)) client.BaseAddress = new Uri(rendererUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Pdf:TimeoutSeconds", 90));
});
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(dataPath, "app.db")}"));

// Keys sign the access cookie; they must survive redeploys, otherwise every invitee is logged out.
builder.Services.AddDataProtection()
    .SetApplicationName("nuxt-cv")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataPath, "keys")));

// The API only sits behind the web container's nginx (which sits behind Coolify's Traefik).
// The right-most X-Forwarded-For entry is the address Traefik saw, i.e. the real client.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var perMinute = builder.Configuration.GetValue("Cv:RedeemPerMinute", 10);
    o.AddPolicy(PublicEndpoints.RedeemRateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.DefaultIgnoreCondition =
    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

app.UseForwardedHeaders();

// Behind a Cloudflare Tunnel every request comes from cloudflared, so X-Forwarded-For holds its
// address. Cloudflare puts the real client into CF-Connecting-IP: set Cv:ClientIpHeader to use it.
var clientIpHeader = app.Configuration["Cv:ClientIpHeader"];
if (!string.IsNullOrEmpty(clientIpHeader))
{
    app.Use((context, next) =>
    {
        if (System.Net.IPAddress.TryParse(context.Request.Headers[clientIpHeader].ToString(), out var ip))
            context.Connection.RemoteIpAddress = ip;
        return next(context);
    });
}
// Works whether or not the reverse proxy strips the /api prefix.
app.UsePathBase("/api");
app.UseRouting();
app.UseRateLimiter();

app.MapPublicEndpoints();
app.MapAdminEndpoints();

app.Run();
return 0;

public partial class Program;
