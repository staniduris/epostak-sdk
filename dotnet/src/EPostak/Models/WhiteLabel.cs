using System.Text.Json.Serialization;

namespace EPostak.Models;

public sealed class WhiteLabelListParticipantsParams
{
    public int? Limit { get; set; }
    public string? Cursor { get; set; }
}

public sealed class WhiteLabelParticipantRegistrationRequest
{
    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("dic")]
    public string Dic { get; set; } = "";

    [JsonPropertyName("companyEmail")]
    public string CompanyEmail { get; set; } = "";

    /// <summary>One-time FS SR secret. Never log this value or the request.</summary>
    [JsonPropertyName("verificationToken")]
    public string VerificationToken { get; set; } = "";
}

public sealed class WhiteLabelParticipantMigrationRequest
{
    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("dic")]
    public string Dic { get; set; } = "";

    [JsonPropertyName("companyEmail")]
    public string CompanyEmail { get; set; } = "";

    /// <summary>SMP migration secret. Never log this value or the request.</summary>
    [JsonPropertyName("migrationCode")]
    public string MigrationCode { get; set; } = "";
}

public sealed class WhiteLabelOperationError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public sealed class WhiteLabelParticipantOperation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("operationType")]
    public string OperationType { get; set; } = "";
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";
    [JsonPropertyName("dic")]
    public string Dic { get; set; } = "";
    [JsonPropertyName("peppolId")]
    public string PeppolId { get; set; } = "";
    [JsonPropertyName("legalName")]
    public string LegalName { get; set; } = "";
    [JsonPropertyName("companyEmail")]
    public string? CompanyEmail { get; set; }
    [JsonPropertyName("firmId")]
    public string? FirmId { get; set; }
    [JsonPropertyName("participantId")]
    public string? ParticipantId { get; set; }
    [JsonPropertyName("reviewRequired")]
    public bool ReviewRequired { get; set; }
    [JsonPropertyName("error")]
    public WhiteLabelOperationError? Error { get; set; }
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";
    [JsonPropertyName("completedAt")]
    public string? CompletedAt { get; set; }
}

public sealed class WhiteLabelParticipant
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";
    [JsonPropertyName("firmId")]
    public string FirmId { get; set; } = "";
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = "";
    [JsonPropertyName("legalName")]
    public string LegalName { get; set; } = "";
    [JsonPropertyName("ico")]
    public string? Ico { get; set; }
    [JsonPropertyName("dic")]
    public string Dic { get; set; } = "";
    [JsonPropertyName("icDph")]
    public string? IcDph { get; set; }
    [JsonPropertyName("peppolId")]
    public string PeppolId { get; set; } = "";
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    [JsonPropertyName("authorizationSource")]
    public string AuthorizationSource { get; set; } = "";
    [JsonPropertyName("endpointProfile")]
    public string EndpointProfile { get; set; } = "";
    [JsonPropertyName("managedSince")]
    public string ManagedSince { get; set; } = "";
}

public sealed class WhiteLabelParticipantList
{
    [JsonPropertyName("participants")]
    public List<WhiteLabelParticipant> Participants { get; set; } = [];
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

public sealed class WhiteLabelMigrationCodeResponse
{
    [JsonPropertyName("operation")]
    public WhiteLabelParticipantOperation Operation { get; set; } = new();
    [JsonPropertyName("migrationCode")]
    public string? MigrationCode { get; set; }
}
