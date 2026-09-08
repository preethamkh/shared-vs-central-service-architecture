using System.Collections.Concurrent;
using System.Text.Json;
using SharedLibrary.Contracts;
using SharedLibrary.Mandrill;

var builder = WebApplication.CreateBuilder(args);
var auditStore = new ConcurrentBag<EmailAuditRecord>();
var app = builder.Build();

var mandrillKey = builder.Configuration["Mandrill:ApiKey"] ?? Environment.GetEnvironmentVariable("MANDRILL_API_KEY");
var fromEmail = builder.Configuration["Mandrill:FromEmail"] ?? "noreply@example.com";
var apiKey = builder.Configuration["ApiKeys:demo"] ?? "demo-key";

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    approach = "shared-library",
    app = "Assessment Portal",
    mode = string.IsNullOrWhiteSpace(mandrillKey) ? "simulation" : "live"
}));

app.MapPost("/api/assessment/email/send", async (HttpRequest httpRequest, EmailRequest request) =>
{
    if (httpRequest.Headers["X-Api-Key"] != apiKey)
        return Results.Unauthorized();
    if (request.To.Count == 0 || string.IsNullOrWhiteSpace(request.TemplateKey))
        return Results.BadRequest(new { error = "TemplateKey and at least one recipient required." });

    var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString("N");
    EmailSendResult result;
    if (string.IsNullOrWhiteSpace(mandrillKey))
    {
        result = new EmailSendResult(true, "simulated", correlationId, $"sim-{correlationId[..8]}", request.SourceSystem, request.TemplateKey);
    }
    else
    {
        using var httpClient = new HttpClient();
        var sender = new MandrillEmailSender(httpClient, mandrillKey, fromEmail);
        result = await sender.SendTemplateAsync(request with { CorrelationId = correlationId }, request.TemplateKey);
    }

    foreach (var recipient in request.To)
    {
        auditStore.Add(new EmailAuditRecord(DateTimeOffset.UtcNow, result.CorrelationId, request.SourceSystem,
            request.TemplateKey, recipient.Email, result.Status, result.ProviderMessageId, result.Error));
    }
    return Results.Accepted($"/api/assessment/activity/{result.CorrelationId}", result);
});

app.MapGet("/api/assessment/activity", (HttpRequest httpRequest, string? search, string? status) =>
{
    if (httpRequest.Headers["X-Api-Key"] != apiKey)
        return Results.Unauthorized();
    var results = auditStore.Where(item =>
        (string.IsNullOrWhiteSpace(search) || JsonSerializer.Serialize(item).Contains(search, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(status) || item.Status.Equals(status, StringComparison.OrdinalIgnoreCase)))
        .OrderByDescending(item => item.OccurredAt);
    return Results.Ok(new { count = results.Count(), items = results, scope = "Assessment only" });
});

app.MapPost("/api/assessment/events/mandrill", (JsonElement events) =>
{
    return Results.Ok(new { received = true, app = "Assessment", note = "Each app handles webhooks independently" });
});

app.MapGet("/api/assessment/templates", (HttpRequest httpRequest) =>
{
    if (httpRequest.Headers["X-Api-Key"] != apiKey)
        return Results.Unauthorized();
    return Results.Ok(new[]
    {
        new { key = "AssessmentBooked", slug = "assessment-booked", owner = "Assessment" },
        new { key = "Welcome", slug = "welcome", owner = "Assessment" }
    });
});

app.MapGet("/", () => Results.Content(GetSupportPage(), "text/html"));
app.Run();

static string GetSupportPage() => """
<!doctype html><html><head><meta charset="utf-8"><title>Assessment Email Audit (Shared Library)</title>
<style>body{font:16px system-ui;max-width:1100px;margin:40px auto;padding:0 20px;color:#17202a}
.warn{background:#fff3cd;border:1px solid #ffc107;padding:12px;border-radius:6px;margin-bottom:20px}
table{border-collapse:collapse;width:100%;margin-top:20px}th,td{border-bottom:1px solid #ddd;text-align:left;padding:10px}
.tag{font-weight:600;color:#126b45}input{padding:9px;margin-right:8px}button{padding:9px 14px}</style></head>
<body>
<h1>Assessment Portal — Email Audit</h1>
<div class="warn"><strong>Shared Library Approach:</strong> This view shows ONLY Assessment system emails.
Accreditation, D365, and Power Automate emails are in their OWN separate audit stores.</div>
<p><input id="search" placeholder="recipient, template"><input id="status" placeholder="status"><button onclick="load()">Search</button></p>
<table><thead><tr><th>Time</th><th>Source</th><th>Template</th><th>Recipient</th><th>Status</th><th>Correlation</th></tr></thead><tbody id="rows"></tbody></table>
<script>async function load(){const q=new URLSearchParams();if(search.value)q.set('search',search.value);if(status.value)q.set('status',status.value);const r=await fetch('/api/assessment/activity?'+q,{headers:{'X-Api-Key':'demo-key'}});const d=await r.json();rows.innerHTML=d.items.map(x=>`<tr><td>${x.occurredAt}</td><td>${x.sourceSystem}</td><td>${x.templateKey}</td><td>${x.recipient}</td><td class="tag">${x.status}</td><td>${x.correlationId}</td></tr>`).join('')}load()</script></body></html>
""";
