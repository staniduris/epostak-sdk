using System.Text.Json.Serialization;

namespace EPostak.Models;

/// <summary>Request body for <c>POST /connector/preflight</c>.</summary>
public sealed class ConnectorPreflightRequest
{
    [JsonPropertyName("receiverPeppolId")]
    public string ReceiverPeppolId { get; set; } = "";

    [JsonPropertyName("document")]
    public Dictionary<string, object?> Document { get; set; } = [];
}

/// <summary>Request body for <c>POST /connector/send</c>.</summary>
public sealed class ConnectorSendRequest
{
    [JsonPropertyName("document")]
    public Dictionary<string, object?>? Document { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> Data { get; set; } = [];
}

/// <summary>
/// Autopilot submit payload kept for source compatibility. New
/// business-document integrations use <see cref="ConnectorBusinessDocumentRequest"/>
/// with a customer-scoped Documents resource.
/// </summary>
public sealed class ConnectorSubmitDocumentRequest
{
    [JsonPropertyName("customerRef")]
    public string? CustomerRef { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    [JsonPropertyName("send")]
    public ConnectorSendPolicyOptions? Send { get; set; }

    [JsonPropertyName("options")]
    public Dictionary<string, object?>? Options { get; set; }
}

/// <summary>Strict OpenAPI business request for customer Documents.Send/Stage.</summary>
public sealed class ConnectorBusinessDocumentRequest
{
    [JsonPropertyName("customerRef")]
    public string? CustomerRef { get; internal set; }

    [JsonPropertyName("delivery")]
    public string? Delivery { get; internal set; }

    [JsonPropertyName("externalId")]
    public string ExternalId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "invoice";

    [JsonPropertyName("number")]
    public string Number { get; set; } = "";

    [JsonPropertyName("precedingDocumentNumber")]
    public string? PrecedingDocumentNumber { get; set; }

    [JsonPropertyName("recipient")]
    public ConnectorBusinessRecipient Recipient { get; set; } = new();

    [JsonPropertyName("issueDate")]
    public string? IssueDate { get; set; }

    [JsonPropertyName("dueDate")]
    public string? DueDate { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("iban")]
    public string? Iban { get; set; }

    [JsonPropertyName("paymentMethod")]
    public string? PaymentMethod { get; set; }

    [JsonPropertyName("variableSymbol")]
    public string? VariableSymbol { get; set; }

    [JsonPropertyName("buyerReference")]
    public string? BuyerReference { get; set; }

    [JsonPropertyName("prepaidAmount")]
    public decimal? PrepaidAmount { get; set; }

    [JsonPropertyName("prepayments")]
    public List<ConnectorBusinessPrepayment> Prepayments { get; set; } = [];

    [JsonPropertyName("lines")]
    public List<ConnectorBusinessLine> Lines { get; set; } = [];

    [JsonPropertyName("attachments")]
    public List<ConnectorBusinessAttachment> Attachments { get; set; } = [];
}

public sealed class ConnectorBusinessRecipient
{
    [JsonPropertyName("country")]
    public string Country { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("companyId")]
    public string? CompanyId { get; set; }

    [JsonPropertyName("taxId")]
    public string? TaxId { get; set; }

    [JsonPropertyName("vatId")]
    public string? VatId { get; set; }

    [JsonPropertyName("networkId")]
    public string? NetworkId { get; set; }

    [JsonPropertyName("address")]
    public ConnectorBusinessAddress? Address { get; set; }
}

public sealed class ConnectorBusinessAddress
{
    [JsonPropertyName("street")]
    public string? Street { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }
}

public sealed class ConnectorBusinessLine
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("vatRate")]
    public decimal VatRate { get; set; }

    [JsonPropertyName("taxTreatment")]
    public string? TaxTreatment { get; set; }

    [JsonPropertyName("discount")]
    public decimal? Discount { get; set; }

    [JsonPropertyName("deliveryDate")]
    public string? DeliveryDate { get; set; }

    [JsonPropertyName("lineType")]
    public string? LineType { get; set; }

    [JsonPropertyName("advanceInvoiceReference")]
    public string? AdvanceInvoiceReference { get; set; }

    [JsonPropertyName("customsTariffCode")]
    public string? CustomsTariffCode { get; set; }

    [JsonPropertyName("commodityClassificationCode")]
    public string? CommodityClassificationCode { get; set; }

    [JsonPropertyName("commodityClassificationListId")]
    public string? CommodityClassificationListId { get; set; }

    [JsonPropertyName("reverseChargeParagraphLetter")]
    public string? ReverseChargeParagraphLetter { get; set; }

    [JsonPropertyName("controlStatementType")]
    public string? ControlStatementType { get; set; }

    [JsonPropertyName("controlStatementQuantity")]
    public decimal? ControlStatementQuantity { get; set; }

    [JsonPropertyName("controlStatementUnit")]
    public string? ControlStatementUnit { get; set; }
}

public sealed class ConnectorBusinessPrepayment
{
    [JsonPropertyName("advanceInvoiceRef")]
    public string? AdvanceInvoiceRef { get; set; }

    [JsonPropertyName("taxDocumentRef")]
    public string? TaxDocumentRef { get; set; }

    [JsonPropertyName("settlementDate")]
    public string? SettlementDate { get; set; }

    [JsonPropertyName("amountWithoutVat")]
    public decimal? AmountWithoutVat { get; set; }

    [JsonPropertyName("vatAmount")]
    public decimal? VatAmount { get; set; }

    [JsonPropertyName("amountWithVat")]
    public decimal AmountWithVat { get; set; }

    [JsonPropertyName("vatRate")]
    public decimal? VatRate { get; set; }

    [JsonPropertyName("taxTreatment")]
    public string? TaxTreatment { get; set; }
}

public sealed class ConnectorBusinessAttachment
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class ConnectorBusinessDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("replayed")]
    public bool? Replayed { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("amounts")]
    public ConnectorBusinessAmounts? Amounts { get; set; }

    [JsonPropertyName("sender")]
    public ConnectorBusinessParty? Sender { get; set; }

    [JsonPropertyName("recipient")]
    public ConnectorBusinessParty? Recipient { get; set; }

    [JsonPropertyName("issueDate")]
    public string? IssueDate { get; set; }

    [JsonPropertyName("dueDate")]
    public string? DueDate { get; set; }

    [JsonPropertyName("processedAt")]
    public string? ProcessedAt { get; set; }

    [JsonPropertyName("processedReference")]
    public string? ProcessedReference { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("response")]
    public ConnectorBusinessInvoiceResponse? Response { get; set; }

    [JsonPropertyName("links")]
    public Dictionary<string, string> Links { get; set; } = [];
}

/// <summary>Latest business-level invoice response projected on list/detail results.</summary>
public sealed class ConnectorBusinessInvoiceResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("respondedAt")]
    public string? RespondedAt { get; set; }
}

public sealed class ConnectorBusinessAmounts
{
    [JsonPropertyName("withoutTax")]
    public decimal? WithoutTax { get; set; }

    [JsonPropertyName("tax")]
    public decimal? Tax { get; set; }

    [JsonPropertyName("total")]
    public decimal? Total { get; set; }

    [JsonPropertyName("due")]
    public decimal? Due { get; set; }
}

public sealed class ConnectorBusinessParty
{
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

    [JsonPropertyName("resolution")]
    public string? Resolution { get; set; }
}

public sealed class ConnectorBusinessDocumentListParams
{
    public string? Direction { get; set; }
    public string? State { get; set; }
    public string? Type { get; set; }
    public string? CreatedAfter { get; set; }
    public string? Cursor { get; set; }
    public int? Limit { get; set; }
}

public sealed class ConnectorBusinessDocumentListResponse
{
    [JsonPropertyName("documents")]
    public List<ConnectorBusinessDocument> Documents { get; set; } = [];

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

public sealed class ConnectorBusinessAcknowledgeResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("processedAt")]
    public string ProcessedAt { get; set; } = "";

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = "";

    [JsonPropertyName("idempotent")]
    public bool Idempotent { get; set; }
}

/// <summary>Business-level response to an inbound Connector invoice.</summary>
public sealed class ConnectorInvoiceResponseRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public sealed class ConnectorInvoiceResponseDelivery
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "";

    [JsonPropertyName("delivery")]
    public string Delivery { get; set; } = "";

    [JsonPropertyName("respondedAt")]
    public string RespondedAt { get; set; } = "";
}

public sealed class ConnectorInvoiceResponseResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("response")]
    public ConnectorInvoiceResponseDelivery Response { get; set; } = new();

    [JsonPropertyName("idempotent")]
    public bool Idempotent { get; set; }
}

public sealed class ConnectorRepairItem
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("autoFixable")]
    public bool AutoFixable { get; set; }
}

public sealed class ConnectorSafeFix
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("applied")]
    public bool Applied { get; set; }
}

public sealed class ConnectorRepairReport
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("blocking")]
    public List<ConnectorRepairItem> Blocking { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<ConnectorRepairItem> Warnings { get; set; } = [];
}

public sealed class ConnectorPreflightResponse
{
    [JsonPropertyName("ready")]
    public bool Ready { get; set; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = "";

    [JsonPropertyName("repairReport")]
    public ConnectorRepairReport RepairReport { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<ConnectorRepairItem> Warnings { get; set; } = [];

    [JsonPropertyName("safeFixes")]
    public List<ConnectorSafeFix> SafeFixes { get; set; } = [];

    [JsonPropertyName("recipient")]
    public Dictionary<string, object?>? Recipient { get; set; }

    [JsonPropertyName("documentProfile")]
    public Dictionary<string, object?>? DocumentProfile { get; set; }

    [JsonPropertyName("checks")]
    public Dictionary<string, object?>? Checks { get; set; }
}

public sealed class ConnectorSendResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }

    [JsonPropertyName("links")]
    public Dictionary<string, object?>? Links { get; set; }
}

public sealed class ConnectorEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("documentId")]
    public string? DocumentId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("occurredAt")]
    public string? OccurredAt { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object?>? Data { get; set; }
}

/// <summary>Canonical customer-scoped Connector business event.</summary>
public sealed class ConnectorBusinessEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("occurredAt")]
    public string OccurredAt { get; set; } = "";

    [JsonPropertyName("data")]
    public ConnectorBusinessEventData Data { get; set; } = new();

    [JsonPropertyName("test")]
    public bool? Test { get; set; }
}

/// <summary>Business-only event data shared by Connector polling and push webhooks.</summary>
public sealed class ConnectorBusinessEventData
{
    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("response")]
    public ConnectorBusinessInvoiceResponse? Response { get; set; }

    /// <summary>Dictionary-style compatibility accessor for existing event consumers.</summary>
    public object? this[string key] => key switch
    {
        "customerRef" => CustomerRef,
        "direction" => Direction,
        "type" => Type,
        "number" => Number,
        "response" => Response,
        _ => null,
    };

    public bool ContainsKey(string key)
        => key is "customerRef" or "direction" or "type" or "number" or "response";
}

public sealed class ConnectorWebhook
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("events")]
    public List<string> Events { get; set; } = [];

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("failedAttempts")]
    public int FailedAttempts { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";
}

public sealed class ConnectorWebhookConfiguration
{
    [JsonPropertyName("webhook")]
    public ConnectorWebhook? Webhook { get; set; }

    [JsonPropertyName("secret")]
    public string? Secret { get; set; }
}

public sealed class ConnectorWebhookTestResponse
{
    [JsonPropertyName("deliveryId")]
    public string DeliveryId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("event")]
    public ConnectorBusinessEvent Event { get; set; } = new();
}

public sealed class ConnectorWebhookDelivery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("webhookId")]
    public string WebhookId { get; set; } = "";

    [JsonPropertyName("eventId")]
    public string? EventId { get; set; }

    [JsonPropertyName("customerRef")]
    public string? CustomerRef { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }

    [JsonPropertyName("responseStatus")]
    public int? ResponseStatus { get; set; }

    [JsonPropertyName("responseTimeMs")]
    public int? ResponseTimeMs { get; set; }

    [JsonPropertyName("lastAttemptAt")]
    public string? LastAttemptAt { get; set; }

    [JsonPropertyName("nextRetryAt")]
    public string? NextRetryAt { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("documentId")]
    public string? DocumentId { get; set; }

    [JsonPropertyName("test")]
    public bool? Test { get; set; }

    [JsonPropertyName("testScenario")]
    public string? TestScenario { get; set; }

    [JsonPropertyName("diagnosisCode")]
    public string? DiagnosisCode { get; set; }

    [JsonPropertyName("nextAction")]
    public string? NextAction { get; set; }

    [JsonPropertyName("replayedFromId")]
    public string? ReplayedFromId { get; set; }

    [JsonPropertyName("canReplay")]
    public bool? CanReplay { get; set; }

    [JsonPropertyName("attemptHistoryComplete")]
    public bool? AttemptHistoryComplete { get; set; }

    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}

public sealed class ConnectorWebhookDeliveriesResponse
{
    [JsonPropertyName("deliveries")]
    public List<ConnectorWebhookDelivery> Deliveries { get; set; } = [];

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

public sealed class ConnectorWebhookDeliveryAttempt
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("outcome")] public string Outcome { get; set; } = "";
    [JsonPropertyName("startedAt")] public string StartedAt { get; set; } = "";
    [JsonPropertyName("completedAt")] public string? CompletedAt { get; set; }
    [JsonPropertyName("durationMs")] public int? DurationMs { get; set; }
    [JsonPropertyName("endpoint")] public string? Endpoint { get; set; }
    [JsonPropertyName("requestTimestamp")] public string? RequestTimestamp { get; set; }
    [JsonPropertyName("requestBodySha256")] public string? RequestBodySha256 { get; set; }
    [JsonPropertyName("responseStatus")] public int? ResponseStatus { get; set; }
    [JsonPropertyName("responseContentType")] public string? ResponseContentType { get; set; }
    [JsonPropertyName("responseBody")] public string? ResponseBody { get; set; }
    [JsonPropertyName("responseBodySha256")] public string? ResponseBodySha256 { get; set; }
    [JsonPropertyName("responseBodyTruncated")] public bool ResponseBodyTruncated { get; set; }
    [JsonPropertyName("retryable")] public bool? Retryable { get; set; }
    [JsonPropertyName("retryAfterMs")] public int? RetryAfterMs { get; set; }
    [JsonPropertyName("nextRetryAt")] public string? NextRetryAt { get; set; }
    [JsonPropertyName("diagnosisCode")] public string? DiagnosisCode { get; set; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; set; }
}

public sealed class ConnectorWebhookDeliveryDetail
{
    [JsonPropertyName("delivery")] public ConnectorWebhookDelivery Delivery { get; set; } = new();
    [JsonPropertyName("payload")] public Dictionary<string, object?> Payload { get; set; } = [];
    [JsonPropertyName("rawBody")] public string? RawBody { get; set; }
    [JsonPropertyName("rawBodySha256")] public string? RawBodySha256 { get; set; }
    [JsonPropertyName("attemptHistoryComplete")] public bool AttemptHistoryComplete { get; set; }
    [JsonPropertyName("endpoint")] public string? Endpoint { get; set; }
    [JsonPropertyName("signature")] public Dictionary<string, object?> Signature { get; set; } = [];
    [JsonPropertyName("attempts")] public List<ConnectorWebhookDeliveryAttempt> Attempts { get; set; } = [];
}

public sealed class ConnectorWebhookReplayResult
{
    [JsonPropertyName("accepted")] public bool Accepted { get; set; }
    [JsonPropertyName("deduplicated")] public bool Deduplicated { get; set; }
    [JsonPropertyName("replayedFrom")] public string ReplayedFrom { get; set; } = "";
    [JsonPropertyName("deliveryId")] public string DeliveryId { get; set; } = "";
    [JsonPropertyName("webhookId")] public string WebhookId { get; set; } = "";
    [JsonPropertyName("eventId")] public string? EventId { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("links")] public Dictionary<string, string> Links { get; set; } = [];
}

public sealed class ConnectorWebhookTestSuiteAccepted
{
    [JsonPropertyName("testRunId")] public string TestRunId { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("deduplicated")] public bool Deduplicated { get; set; }
    [JsonPropertyName("deliveryIds")] public List<string> DeliveryIds { get; set; } = [];
    [JsonPropertyName("expiresAt")] public string ExpiresAt { get; set; } = "";
    [JsonPropertyName("links")] public Dictionary<string, string> Links { get; set; } = [];
}

public sealed class ConnectorWebhookTestSuiteStatus
{
    [JsonPropertyName("testRunId")] public string TestRunId { get; set; } = "";
    [JsonPropertyName("event")] public string Event { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("scenarios")] public List<ConnectorWebhookTestSuiteScenario> Scenarios { get; set; } = [];
    [JsonPropertyName("createdAt")] public string CreatedAt { get; set; } = "";
    [JsonPropertyName("expiresAt")] public string ExpiresAt { get; set; } = "";
    [JsonPropertyName("links")] public Dictionary<string, string> Links { get; set; } = [];
}

public sealed class ConnectorWebhookTestSuiteScenario
{
    [JsonPropertyName("scenario")] public string Scenario { get; set; } = "";
    [JsonPropertyName("complete")] public bool Complete { get; set; }
    [JsonPropertyName("passed")] public bool? Passed { get; set; }
    [JsonPropertyName("failureReason")] public string? FailureReason { get; set; }
    [JsonPropertyName("actionRequired")] public string? ActionRequired { get; set; }
    [JsonPropertyName("replayDeliveryId")] public string? ReplayDeliveryId { get; set; }
    [JsonPropertyName("deliveries")] public List<ConnectorWebhookTestSuiteDelivery> Deliveries { get; set; } = [];
}

public sealed class ConnectorWebhookTestSuiteDelivery
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("attempts")] public int Attempts { get; set; }
    [JsonPropertyName("responseStatus")] public int? ResponseStatus { get; set; }
    [JsonPropertyName("nextRetryAt")] public string? NextRetryAt { get; set; }
    [JsonPropertyName("detail")] public string Detail { get; set; } = "";
}

public sealed class ConnectorStatusResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("deliveredAt")]
    public string? DeliveredAt { get; set; }

    [JsonPropertyName("events")]
    public List<ConnectorEvent> Events { get; set; } = [];
}

public sealed class ConnectorInboxDocument
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("documentNumber")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("senderPeppolId")]
    public string? SenderPeppolId { get; set; }

    [JsonPropertyName("receiverPeppolId")]
    public string? ReceiverPeppolId { get; set; }

    [JsonPropertyName("acknowledgedAt")]
    public string? AcknowledgedAt { get; set; }

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("payloadFormat")]
    public string? PayloadFormat { get; set; }

    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}

public sealed class ConnectorListParams
{
    public string? Cursor { get; set; }
    public int? Limit { get; set; }
}

public sealed class ConnectorInboxListResponse
{
    [JsonPropertyName("documents")]
    public List<ConnectorInboxDocument> Documents { get; set; } = [];

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

public sealed class ConnectorAckResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("acknowledged")]
    public bool Acknowledged { get; set; }

    [JsonPropertyName("idempotent")]
    public bool? Idempotent { get; set; }

    [JsonPropertyName("acknowledgedAt")]
    public string? AcknowledgedAt { get; set; }
}

public sealed class ConnectorEventsResponse
{
    [JsonPropertyName("events")]
    public List<ConnectorEvent> Events { get; set; } = [];

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

/// <summary>Canonical customer-scoped business event page.</summary>
public sealed class ConnectorBusinessEventsResponse
{
    [JsonPropertyName("events")]
    public List<ConnectorBusinessEvent> Events { get; set; } = [];

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

/// <summary>Request body for <c>POST /connector/autopilot</c>.</summary>
public sealed class ConnectorAutopilotRequest
{
    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "shadow";

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("payload")]
    public Dictionary<string, object?> Payload { get; set; } = [];

    [JsonPropertyName("send")]
    public ConnectorSendPolicyOptions? Send { get; set; }

    [JsonPropertyName("options")]
    public Dictionary<string, object?>? Options { get; set; }
}

public sealed class ConnectorAutopilotRunResponse
{
    [JsonPropertyName("autopilotId")]
    public string AutopilotId { get; set; } = "";

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";

    [JsonPropertyName("lifecycleStatus")]
    public string LifecycleStatus { get; set; } = "";

    [JsonPropertyName("replayed")]
    public bool Replayed { get; set; }

    [JsonPropertyName("preflight")]
    public Dictionary<string, object?>? Preflight { get; set; }

    [JsonPropertyName("repairReport")]
    public Dictionary<string, object?>? RepairReport { get; set; }

    [JsonPropertyName("safeFixes")]
    public List<ConnectorSafeFix> SafeFixes { get; set; } = [];

    [JsonPropertyName("send")]
    public Dictionary<string, object?>? Send { get; set; }

    [JsonPropertyName("status")]
    public Dictionary<string, object?>? Status { get; set; }

    [JsonPropertyName("lastError")]
    public Dictionary<string, object?>? LastError { get; set; }

    [JsonPropertyName("documentId")]
    public string? DocumentId { get; set; }

    [JsonPropertyName("outboxId")]
    public string? OutboxId { get; set; }

    [JsonPropertyName("sentAt")]
    public string? SentAt { get; set; }

    [JsonPropertyName("deliveredAt")]
    public string? DeliveredAt { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("nextActions")]
    public List<string> NextActions { get; set; } = [];

    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}

public sealed class ConnectorMapperRequest
{
    [JsonPropertyName("templateKey")]
    public string? TemplateKey { get; set; }

    [JsonPropertyName("sourceType")]
    public string? SourceType { get; set; }

    [JsonPropertyName("sourceText")]
    public string? SourceText { get; set; }

    [JsonPropertyName("sourceJson")]
    public Dictionary<string, object?>? SourceJson { get; set; }

    [JsonPropertyName("customerRef")]
    public string? CustomerRef { get; set; }

    [JsonPropertyName("execute")]
    public string? Execute { get; set; }

    [JsonPropertyName("confirmed")]
    public bool? Confirmed { get; set; }

    [JsonPropertyName("fieldMap")]
    public Dictionary<string, object?>? FieldMap { get; set; }

    [JsonPropertyName("defaults")]
    public Dictionary<string, object?>? Defaults { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = [];
}

public sealed class ConnectorReconcileParams
{
    public string? Status { get; set; }
    public string? Since { get; set; }
}

public sealed class ConnectorReconcileItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("lifecycleStatus")]
    public string LifecycleStatus { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("repairReport")]
    public Dictionary<string, object?>? RepairReport { get; set; }

    [JsonPropertyName("lastError")]
    public Dictionary<string, object?>? LastError { get; set; }

    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}

public sealed class ConnectorReconcileResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("since")]
    public string? Since { get; set; }

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = "";

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<ConnectorReconcileItem> Items { get; set; } = [];
}

public sealed class ConnectorSendPolicyOptions
{
    [JsonPropertyName("policy")]
    public string Policy { get; set; } = "";

    [JsonPropertyName("sendAt")]
    public string? SendAt { get; set; }
}

public sealed class ConnectorZenInputRequest
{
    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("previewOnly")]
    public bool? PreviewOnly { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }

    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonPropertyName("receiverPeppolId")]
    public string? ReceiverPeppolId { get; set; }

    [JsonPropertyName("receiver")]
    public Dictionary<string, object?>? Receiver { get; set; }

    [JsonPropertyName("buyer")]
    public Dictionary<string, object?>? Buyer { get; set; }

    [JsonPropertyName("customer")]
    public Dictionary<string, object?>? Customer { get; set; }

    [JsonPropertyName("lines")]
    public List<Dictionary<string, object?>>? Lines { get; set; }

    [JsonPropertyName("items")]
    public List<Dictionary<string, object?>>? Items { get; set; }

    [JsonPropertyName("send")]
    public ConnectorSendPolicyOptions? Send { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = [];
}

public sealed class ConnectorMailboxListResponse
{
    [JsonPropertyName("mailboxes")]
    public List<Dictionary<string, object?>> Mailboxes { get; set; } = [];
}

public sealed class ConnectorMailboxRepairRequest
{
    [JsonPropertyName("customerRef")]
    public string? CustomerRef { get; set; }
}

public sealed class ConnectorMailboxUpdateResponse
{
    [JsonPropertyName("mailbox")]
    public Dictionary<string, object?> Mailbox { get; set; } = [];
}

public sealed class ConnectorSyncParams
{
    public string? CustomerRef { get; set; }
    public string? Cursor { get; set; }
    public int? Limit { get; set; }
}

public sealed class ConnectorSyncResponse
{
    [JsonPropertyName("items")]
    public List<Dictionary<string, object?>> Items { get; set; } = [];

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

public sealed class ConnectorActionRequest
{
    [JsonPropertyName("sendAt")]
    public string? SendAt { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = [];
}

public sealed class ConnectorActionResponse
{
    [JsonPropertyName("action")]
    public Dictionary<string, object?> Action { get; set; } = [];
}

/// <summary>One item accepted by <c>POST /connector/outbox</c>.</summary>
public sealed class ConnectorOutboxStageItem
{
    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("scheduledFor")]
    public string? ScheduledFor { get; set; }

    [JsonPropertyName("payload")]
    public Dictionary<string, object?> Payload { get; set; } = [];
}

/// <summary>Request body for <c>POST /connector/outbox</c>.</summary>
public sealed class ConnectorOutboxStageRequest
{
    [JsonPropertyName("items")]
    public List<ConnectorOutboxStageItem>? Items { get; set; }

    [JsonPropertyName("payload")]
    public Dictionary<string, object?>? Payload { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("scheduledFor")]
    public string? ScheduledFor { get; set; }
}

public sealed class ConnectorOutboxItem
{
    [JsonPropertyName("outboxId")]
    public string OutboxId { get; set; } = "";

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("scheduledFor")]
    public string? ScheduledFor { get; set; }

    [JsonPropertyName("documentId")]
    public string? DocumentId { get; set; }

    [JsonPropertyName("ready")]
    public bool Ready { get; set; }

    [JsonPropertyName("repairReport")]
    public ConnectorRepairReport? RepairReport { get; set; }

    [JsonPropertyName("safeFixes")]
    public List<ConnectorSafeFix> SafeFixes { get; set; } = [];

    [JsonPropertyName("lastError")]
    public Dictionary<string, object?>? LastError { get; set; }

    [JsonPropertyName("attemptCount")]
    public int AttemptCount { get; set; }

    [JsonPropertyName("sentAt")]
    public string? SentAt { get; set; }

    [JsonPropertyName("cancelledAt")]
    public string? CancelledAt { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}

public sealed class ConnectorOutboxStageResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("ready")]
    public int? Ready { get; set; }

    [JsonPropertyName("blocked")]
    public int? Blocked { get; set; }

    [JsonPropertyName("staged")]
    public int? Staged { get; set; }

    [JsonPropertyName("items")]
    public List<ConnectorOutboxItem> Items { get; set; } = [];
}

public sealed class ConnectorOutboxListParams
{
    public string? Status { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public sealed class ConnectorOutboxListResponse
{
    [JsonPropertyName("items")]
    public List<ConnectorOutboxItem> Items { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

public sealed class ConnectorOutboxSendOptions
{
    [JsonPropertyName("force")]
    public bool? Force { get; set; }
}

public sealed class ConnectorOutboxBatchSendRequest
{
    [JsonPropertyName("ids")]
    public List<string>? Ids { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("force")]
    public bool? Force { get; set; }
}

public sealed class ConnectorOutboxBatchSendResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("sent")]
    public int Sent { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [JsonPropertyName("results")]
    public List<ConnectorOutboxItem> Results { get; set; } = [];
}
