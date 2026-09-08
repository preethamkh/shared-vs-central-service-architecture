namespace SharedLibrary.Contracts;

public sealed record EmailRecipient(string Email, string? Name = null);

public sealed record EmailRequest(
    string TemplateKey,
    IReadOnlyList<EmailRecipient> To,
    IReadOnlyDictionary<string, object?> Data,
    string SourceSystem,
    string? CorrelationId = null);

public sealed record EmailSendResult(
    bool Accepted,
    string Status,
    string CorrelationId,
    string? ProviderMessageId,
    string SourceSystem,
    string TemplateKey,
    string? Error = null);

public sealed record EmailAuditRecord(
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string SourceSystem,
    string TemplateKey,
    string Recipient,
    string Status,
    string? ProviderMessageId,
    string? Error);
