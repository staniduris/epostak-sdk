using System.Text.Json.Serialization;

namespace EPostak.Models;

public sealed class CreateConsentOfferRequest
{
    [JsonPropertyName("targetIdentifierType")]
    public string? TargetIdentifierType { get; set; }
    [JsonPropertyName("targetIdentifier")]
    public string? TargetIdentifier { get; set; }
    [JsonPropertyName("customerReference")]
    public string? CustomerReference { get; set; }
    [JsonPropertyName("integrationPath")]
    public string? IntegrationPath { get; set; }
    [JsonPropertyName("relationshipMode")]
    public string? RelationshipMode { get; set; }
    [JsonPropertyName("scopes")]
    public List<String> Scopes { get; set; } = [];
}

public sealed class ConsentOfferResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("consentUrl")]
    public string? ConsentUrl { get; set; }
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    [JsonPropertyName("expiresAt")]
    public string? ExpiresAt { get; set; }
    [JsonPropertyName("customerReference")]
    public string? CustomerReference { get; set; }
    [JsonPropertyName("integrationPath")]
    public string? IntegrationPath { get; set; }
    [JsonPropertyName("relationshipMode")]
    public string? RelationshipMode { get; set; }
    [JsonPropertyName("scopes")]
    public List<String> Scopes { get; set; } = [];
    [JsonPropertyName("acceptedAt")]
    public string? AcceptedAt { get; set; }
    [JsonPropertyName("revokedAt")]
    public string? RevokedAt { get; set; }
}

public sealed class WhiteLabelCustomerAuthorization
{
    [JsonPropertyName("confirmed")]
    public bool? Confirmed { get; set; }
    [JsonPropertyName("evidenceReference")]
    public string? EvidenceReference { get; set; }
}

public sealed class WhiteLabelCustomerCreateRequest
{
    [JsonPropertyName("customerRef")]
    public string? CustomerRef { get; set; }
    [JsonPropertyName("relationship")]
    public string? Relationship { get; set; }
    [JsonPropertyName("country")]
    public string? Country { get; set; }
    [JsonPropertyName("companyId")]
    public string? CompanyId { get; set; }
    [JsonPropertyName("taxId")]
    public string? TaxId { get; set; }
    [JsonPropertyName("vatId")]
    public string? VatId { get; set; }
    [JsonPropertyName("contactEmail")]
    public string? ContactEmail { get; set; }
    [JsonPropertyName("returnUrl")]
    public string? ReturnUrl { get; set; }
    [JsonPropertyName("customerAuthorization")]
    public WhiteLabelCustomerAuthorization? CustomerAuthorization { get; set; }
}

public sealed class WhiteLabelCustomerActivation
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    [JsonPropertyName("expiresAt")]
    public string? ExpiresAt { get; set; }
}

public sealed class WhiteLabelCustomerNextAction
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class WhiteLabelCustomerPolicy
{
    [JsonPropertyName("sendMode")]
    public string? SendMode { get; set; }
    [JsonPropertyName("ublHandling")]
    public string? UblHandling { get; set; }
    [JsonPropertyName("ocrAutoSend")]
    public bool OcrAutoSend { get; set; }
    [JsonPropertyName("attachSourceFile")]
    public string? AttachSourceFile { get; set; }
    [JsonPropertyName("locale")]
    public string? Locale { get; set; }
    [JsonPropertyName("version")]
    public int Version { get; set; }
}

public sealed class WhiteLabelCustomer
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("firmId")]
    public string? FirmId { get; set; }
    [JsonPropertyName("customerRef")]
    public string? CustomerRef { get; set; }
    [JsonPropertyName("relationship")]
    public string? Relationship { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("country")]
    public string? Country { get; set; }
    [JsonPropertyName("companyId")]
    public string? CompanyId { get; set; }
    [JsonPropertyName("taxId")]
    public string? TaxId { get; set; }
    [JsonPropertyName("vatId")]
    public string? VatId { get; set; }
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    [JsonPropertyName("activation")]
    public WhiteLabelCustomerActivation? Activation { get; set; }
    [JsonPropertyName("nextAction")]
    public WhiteLabelCustomerNextAction? NextAction { get; set; }
    [JsonPropertyName("policy")]
    public WhiteLabelCustomerPolicy? Policy { get; set; }
    [JsonPropertyName("version")]
    public int Version { get; set; }
    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }
}

public sealed class WhiteLabelCustomerList
{
    [JsonPropertyName("customers")]
    public List<WhiteLabelCustomer> Customers { get; set; } = [];
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

public sealed class WhiteLabelListCustomersParams
{
    public int? Limit { get; set; }
    public string? Cursor { get; set; }
    public string? Status { get; set; }
}
