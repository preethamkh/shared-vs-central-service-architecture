using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharedLibrary.Contracts;

namespace SharedLibrary.Mandrill;

/// <summary>
/// This is the "shared library" that each .NET application bundles via NuGet.
/// In the shared-library approach, EVERY application has its own copy of this code,
/// its own Mandrill API key in config, and its own audit store.
/// 
/// PAIN POINTS THIS DEMONSTRATES:
/// - Each app stores Mandrill credentials independently (secret sprawl)
/// - Each app must implement its own audit logging
/// - Each app must handle Mandrill webhooks independently
/// - Template mapping is duplicated across apps
/// - No central correlation ID or support view
/// - D365 and Power Automate CANNOT use this .NET library — they need an HTTP endpoint anyway
/// </summary>
public sealed class MandrillEmailSender(HttpClient httpClient, string apiKey, string fromEmail)
{
    public async Task<EmailSendResult> SendTemplateAsync(EmailRequest request, string templateSlug, CancellationToken cancellationToken = default)
    {
        var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString("N");
        var payload = new
        {
            key = apiKey,
            template_name = templateSlug,
            template_content = Array.Empty<object>(),
            message = new
            {
                from_email = fromEmail,
                subject = $"POC {templateSlug}",
                to = request.To.Select(r => new { email = r.Email, name = r.Name, type = "to" }),
                merge_language = "handlebars",
                global_merge_vars = BuildMergeVars(request.Data)
            },
            @async = false
        };

        using var response = await httpClient.PostAsJsonAsync("https://mandrillapp.com/api/1.0/messages/send-template.json", payload, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        var body = JsonSerializer.Deserialize<JsonElement>(raw);

        MandrillResponse[]? results = null;
        if (body.ValueKind == JsonValueKind.Array)
            results = body.Deserialize<MandrillResponse[]>();
        else if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("status", out var s) && s.GetString() == "error")
        {
            var err = body.Deserialize<MandrillError>();
            return new EmailSendResult(false, "error", correlationId, null, request.SourceSystem, request.TemplateKey, err?.Message ?? "Mandrill error");
        }

        var result = results?.FirstOrDefault();
        var accepted = response.IsSuccessStatusCode && result?.Status is "sent" or "queued";
        return new EmailSendResult(accepted, result?.Status ?? $"http-{(int)response.StatusCode}", correlationId,
            result?._id, request.SourceSystem, request.TemplateKey, accepted ? null : result?.reject_reason ?? response.ReasonPhrase);
    }

    private static IEnumerable<object> BuildMergeVars(IReadOnlyDictionary<string, object?> data)
    {
        var list = new List<object>();
        foreach (var item in data)
        {
            var node = JsonNode.Parse(JsonSerializer.Serialize(item.Value));
            list.Add(new { name = item.Key, content = node });
        }
        return list;
    }

    private sealed class MandrillResponse
    {
        public string? Status { get; set; }
        public string? _id { get; set; }
        public string? reject_reason { get; set; }
    }

    private sealed class MandrillError
    {
        public string? Status { get; set; }
        public int Code { get; set; }
        public string? Name { get; set; }
        public string? Message { get; set; }
    }
}
