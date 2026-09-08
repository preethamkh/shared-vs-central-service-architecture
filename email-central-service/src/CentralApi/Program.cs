using System.Collections.Concurrent;
using System.Text.Json;
using CentralContracts;
using CentralMandrill;
using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);
var auditStore = new ConcurrentBag<EmailAuditRecord>();
var serviceBusConnection = builder.Configuration["ServiceBusConnection"];
var auditQueue = builder.Configuration["EmailAuditQueue"] ?? "email-events";
if (!string.IsNullOrWhiteSpace(serviceBusConnection))
    builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));

var app = builder.Build();
var mandrillKey = builder.Configuration["Mandrill:ApiKey"] ?? Environment.GetEnvironmentVariable("MANDRILL_API_KEY");
var fromEmail = builder.Configuration["Mandrill:FromEmail"] ?? "noreply@example.com";
var apiKey = builder.Configuration["ApiKeys:demo"] ?? "demo-key";

app.MapGet("/health", () => Results.Ok(new { status = "ok", approach = "central-service" }));
app.MapPost("/api/v1/email/send", async (HttpRequest req, EmailRequest request, IServiceProvider svcs) =>
{
    if (req.Headers["X-Api-Key"] != apiKey) return Results.Unauthorized();
    if (request.To.Count == 0 || string.IsNullOrWhiteSpace(request.TemplateKey))
        return Results.BadRequest(new { error = "TemplateKey and at least one recipient required." });
    var cid = request.CorrelationId ?? Guid.NewGuid().ToString("N");
    EmailSendResult result;
    if (string.IsNullOrWhiteSpace(mandrillKey))
        result = new(true, "simulated", cid, $"sim-{cid[..8]}", request.SourceSystem, request.TemplateKey);
    else
    {
        using var hc = new HttpClient();
        result = await new MandrillEmailSender(hc, mandrillKey, fromEmail).SendTemplateAsync(request with { CorrelationId = cid }, request.TemplateKey);
    }
    foreach (var r in request.To)
    {
        var ar = new EmailAuditRecord(DateTimeOffset.UtcNow, result.CorrelationId, request.SourceSystem, request.TemplateKey, r.Email, result.Status, result.ProviderMessageId, result.Error, request.Data);
        auditStore.Add(ar);
        var sbc = svcs.GetService<ServiceBusClient>();
        if (sbc is not null)
        {
            await using var s = sbc.CreateSender(auditQueue);
            await s.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(ar)) { ContentType = "application/json", CorrelationId = result.CorrelationId });
        }
    }
    return Results.Accepted($"/api/v1/activity/{result.CorrelationId}", result);
});
app.MapGet("/api/v1/activity", (HttpRequest req, string? search, string? status, string? source) =>
{
    if (req.Headers["X-Api-Key"] != apiKey) return Results.Unauthorized();
    var results = auditStore.Where(item =>
        (string.IsNullOrWhiteSpace(search) || JsonSerializer.Serialize(item).Contains(search, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(status) || item.Status.Equals(status, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(source) || item.SourceSystem.Equals(source, StringComparison.OrdinalIgnoreCase)))
        .OrderByDescending(item => item.OccurredAt);
    return Results.Ok(new { count = results.Count(), items = results });
});
app.MapPost("/api/v1/events/mandrill", (JsonElement e) => Results.Ok(new { received = true }));
app.MapGet("/api/v1/templates", (HttpRequest req) =>
{
    if (req.Headers["X-Api-Key"] != apiKey) return Results.Unauthorized();
    return Results.Ok(new[] { new { key = "AssessmentBooked", slug = "assessment-booked" }, new { key = "Welcome", slug = "welcome" } });
});
app.MapGet("/", () => Results.Content(GetSupportPage(), "text/html"));
app.Run();

static string GetSupportPage() => """
<!doctype html><html><head><meta charset="utf-8"><title>Central Email Service</title>
<style>body{font:16px system-ui;max-width:1100px;margin:40px auto;padding:0 20px}table{border-collapse:collapse;width:100%;margin-top:20px}th,td{border-bottom:1px solid #ddd;padding:10px}input{padding:9px;margin-right:8px}button{padding:9px 14px}</style></head>
<body><h1>Central Email Service — Unified Audit</h1>
<p><input id="search" placeholder="search"><input id="status" placeholder="status"><button onclick="load()">Search</button></p>
<table><thead><tr><th>Time</th><th>Source</th><th>Template</th><th>Recipient</th><th>Status</th><th>Correlation</th></tr></thead><tbody id="rows"></tbody></table>
<script>async function load(){const q=new URLSearchParams();if(search.value)q.set('search',search.value);if(status.value)q.set('status',status.value);const r=await fetch('/api/v1/activity?'+q,{headers:{'X-Api-Key':'demo-key'}});const d=await r.json();rows.innerHTML=d.items.map(x=>`<tr><td>${x.occurredAt}</td><td>${x.sourceSystem}</td><td>${x.templateKey}</td><td>${x.recipient}</td><td>${x.status}</td><td>${x.correlationId}</td></tr>`).join('')}load()</script></body></html>
""";
