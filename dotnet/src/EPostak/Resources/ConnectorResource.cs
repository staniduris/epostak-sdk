using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Customer-scoped Connector documents and events for ERP teams. Start from
/// <see cref="Customers"/> after ePošťák approves the firm and the integrator
/// stores its own stable customer reference in the dashboard. Lower-level
/// workflows are grouped under <see cref="Advanced"/>.
/// </summary>
public sealed class ConnectorResource
{
    private static readonly HashSet<string> InvoiceResponseStatuses =
    [
        "received",
        "in_process",
        "under_query",
        "conditionally_accepted",
        "rejected",
        "accepted",
        "paid",
    ];

    private static readonly char[] ConnectorTrimChars =
    [
        '\u0009', '\u000A', '\u000B', '\u000C', '\u000D', '\u0020', '\u00A0', '\u1680',
        '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007',
        '\u2008', '\u2009', '\u200A', '\u2028', '\u2029', '\u202F', '\u205F', '\u3000', '\uFEFF',
    ];

    internal static string TrimString(string value) => value.Trim(ConnectorTrimChars);
    private readonly HttpRequestor _http;

    internal ConnectorResource(HttpRequestor http)
    {
        _http = http;
        Documents = new ConnectorDocumentsResource(this);
        Customers = new ConnectorCustomersResource(this);
        Advanced = new ConnectorAdvancedResource(this);
        Webhook = new ConnectorWebhookResource(http);
    }

    public ConnectorDocumentsResource Documents { get; }
    public ConnectorCustomersResource Customers { get; }
    /// <summary>Single global push webhook shared by every managed Connector firm.</summary>
    public ConnectorWebhookResource Webhook { get; }
    /// <summary>Explicit opt-in surface for lower-level and compatibility workflows.</summary>
    public ConnectorAdvancedResource Advanced { get; }

    internal Task<ConnectorBusinessDocument> SubmitCustomerDocumentAsync(
        string customerRef,
        ConnectorBusinessDocumentRequest request,
        string delivery,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        var normalizedCustomerRef = TrimString(customerRef ?? "");
        if (normalizedCustomerRef.Length == 0)
            throw new ArgumentException("Connector customerRef is required.", nameof(customerRef));
        var normalizedExternalId = TrimString(request.ExternalId ?? "");
        if (normalizedExternalId.Length == 0)
            throw new ArgumentException("Connector externalId is required.", nameof(request));
        if (TrimString(request.Number ?? "").Length == 0)
            throw new ArgumentException("Connector number is required.", nameof(request));
        if (TrimString(request.Recipient.Country ?? "").Length == 0)
            throw new ArgumentException("Connector recipient.country is required.", nameof(request));
        if (new[] { request.Recipient.CompanyId, request.Recipient.TaxId, request.Recipient.VatId, request.Recipient.NetworkId }
            .All(value => TrimString(value ?? "").Length == 0))
            throw new ArgumentException("Connector recipient requires companyId, taxId, vatId, or networkId.", nameof(request));
        if (request.Lines.Count == 0)
            throw new ArgumentException("Connector lines must contain at least one item.", nameof(request));
        var transport = ConnectorSubmitDocumentTransport.From(
            normalizedCustomerRef,
            normalizedExternalId,
            delivery,
            request);
        var key = idempotencyKey is null
            ? DefaultIdempotencyKey(normalizedCustomerRef, normalizedExternalId)
            : ValidateIdempotencyKey(idempotencyKey);
        return _http.RequestIdempotentAsync<ConnectorBusinessDocument>(
            HttpMethod.Post,
            "/connector/documents",
            transport,
            key,
            ct,
            omitFirmId: true);
    }

    private static string DefaultIdempotencyKey(string customerRef, string externalId)
    {
        var customerBytes = Encoding.UTF8.GetBytes(TrimString(customerRef));
        var externalBytes = Encoding.UTF8.GetBytes(TrimString(externalId));
        var input = new byte[checked(8 + customerBytes.Length + externalBytes.Length)];

        BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(0, 4), (uint)customerBytes.Length);
        customerBytes.CopyTo(input, 4);
        var externalLengthOffset = 4 + customerBytes.Length;
        BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(externalLengthOffset, 4), (uint)externalBytes.Length);
        externalBytes.CopyTo(input, externalLengthOffset + 4);

        return $"connector:v1:{Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant()}";
    }

    private static string ValidateIdempotencyKey(string value)
    {
        var byteLength = Encoding.UTF8.GetByteCount(value);
        if (TrimString(value).Length == 0 || byteLength > 255)
            throw new ArgumentException("Connector idempotency key must be 1-255 UTF-8 bytes.", nameof(value));
        return value;
    }

    /// <summary>
    /// Immutable, deep request snapshot. Authentication can await before JSON
    /// serialization, so serializing the caller-owned mutable request directly
    /// would let concurrent reuse leak another customer or delivery mode.
    /// </summary>
    private sealed record ConnectorSubmitDocumentTransport(
        [property: JsonPropertyName("customerRef")] string CustomerRef,
        [property: JsonPropertyName("delivery")] string Delivery,
        [property: JsonPropertyName("externalId")] string ExternalId,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("precedingDocumentNumber")] string? PrecedingDocumentNumber,
        [property: JsonPropertyName("recipient")] ConnectorRecipientTransport Recipient,
        [property: JsonPropertyName("issueDate")] string? IssueDate,
        [property: JsonPropertyName("dueDate")] string? DueDate,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("note")] string? Note,
        [property: JsonPropertyName("iban")] string? Iban,
        [property: JsonPropertyName("paymentMethod")] string? PaymentMethod,
        [property: JsonPropertyName("variableSymbol")] string? VariableSymbol,
        [property: JsonPropertyName("buyerReference")] string? BuyerReference,
        [property: JsonPropertyName("prepaidAmount")] decimal? PrepaidAmount,
        [property: JsonPropertyName("prepayments")] IReadOnlyList<ConnectorPrepaymentTransport>? Prepayments,
        [property: JsonPropertyName("lines")] IReadOnlyList<ConnectorLineTransport> Lines,
        [property: JsonPropertyName("attachments")] IReadOnlyList<ConnectorAttachmentTransport> Attachments)
    {
        internal static ConnectorSubmitDocumentTransport From(
            string customerRef,
            string externalId,
            string delivery,
            ConnectorBusinessDocumentRequest request) => new(
                customerRef,
                delivery,
                externalId,
                request.Type,
                request.Number,
                request.PrecedingDocumentNumber,
                ConnectorRecipientTransport.From(request.Recipient),
                request.IssueDate,
                request.DueDate,
                request.Currency,
                request.Note,
                request.Iban,
                request.PaymentMethod,
                request.VariableSymbol,
                request.BuyerReference,
                request.PrepaidAmount,
                request.Prepayments.Count == 0
                    ? null
                    : request.Prepayments.Select(ConnectorPrepaymentTransport.From).ToArray(),
                request.Lines.Select(ConnectorLineTransport.From).ToArray(),
                request.Attachments.Select(ConnectorAttachmentTransport.From).ToArray());
    }

    private sealed record ConnectorRecipientTransport(
        [property: JsonPropertyName("country")] string Country,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("companyId")] string? CompanyId,
        [property: JsonPropertyName("taxId")] string? TaxId,
        [property: JsonPropertyName("vatId")] string? VatId,
        [property: JsonPropertyName("networkId")] string? NetworkId,
        [property: JsonPropertyName("address")] ConnectorAddressTransport? Address)
    {
        internal static ConnectorRecipientTransport From(ConnectorBusinessRecipient value) => new(
            value.Country,
            value.Name,
            value.CompanyId,
            value.TaxId,
            value.VatId,
            value.NetworkId,
            value.Address is null ? null : ConnectorAddressTransport.From(value.Address));
    }

    private sealed record ConnectorAddressTransport(
        [property: JsonPropertyName("street")] string? Street,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("postalCode")] string? PostalCode)
    {
        internal static ConnectorAddressTransport From(ConnectorBusinessAddress value) =>
            new(value.Street, value.City, value.PostalCode);
    }

    private sealed record ConnectorPrepaymentTransport(
        [property: JsonPropertyName("advanceInvoiceRef")] string? AdvanceInvoiceRef,
        [property: JsonPropertyName("taxDocumentRef")] string? TaxDocumentRef,
        [property: JsonPropertyName("settlementDate")] string? SettlementDate,
        [property: JsonPropertyName("amountWithoutVat")] decimal? AmountWithoutVat,
        [property: JsonPropertyName("vatAmount")] decimal? VatAmount,
        [property: JsonPropertyName("amountWithVat")] decimal AmountWithVat,
        [property: JsonPropertyName("vatRate")] decimal? VatRate,
        [property: JsonPropertyName("taxTreatment")] string? TaxTreatment)
    {
        internal static ConnectorPrepaymentTransport From(ConnectorBusinessPrepayment value) => new(
            value.AdvanceInvoiceRef,
            value.TaxDocumentRef,
            value.SettlementDate,
            value.AmountWithoutVat,
            value.VatAmount,
            value.AmountWithVat,
            value.VatRate,
            value.TaxTreatment);
    }

    private sealed record ConnectorLineTransport(
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("quantity")] decimal Quantity,
        [property: JsonPropertyName("unit")] string? Unit,
        [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
        [property: JsonPropertyName("vatRate")] decimal VatRate,
        [property: JsonPropertyName("taxTreatment")] string? TaxTreatment,
        [property: JsonPropertyName("discount")] decimal? Discount,
        [property: JsonPropertyName("deliveryDate")] string? DeliveryDate,
        [property: JsonPropertyName("lineType")] string? LineType,
        [property: JsonPropertyName("advanceInvoiceReference")] string? AdvanceInvoiceReference,
        [property: JsonPropertyName("customsTariffCode")] string? CustomsTariffCode,
        [property: JsonPropertyName("commodityClassificationCode")] string? CommodityClassificationCode,
        [property: JsonPropertyName("commodityClassificationListId")] string? CommodityClassificationListId,
        [property: JsonPropertyName("reverseChargeParagraphLetter")] string? ReverseChargeParagraphLetter,
        [property: JsonPropertyName("controlStatementType")] string? ControlStatementType,
        [property: JsonPropertyName("controlStatementQuantity")] decimal? ControlStatementQuantity,
        [property: JsonPropertyName("controlStatementUnit")] string? ControlStatementUnit)
    {
        internal static ConnectorLineTransport From(ConnectorBusinessLine value) => new(
            value.Description,
            value.Quantity,
            value.Unit,
            value.UnitPrice,
            value.VatRate,
            value.TaxTreatment,
            value.Discount,
            value.DeliveryDate,
            value.LineType,
            value.AdvanceInvoiceReference,
            value.CustomsTariffCode,
            value.CommodityClassificationCode,
            value.CommodityClassificationListId,
            value.ReverseChargeParagraphLetter,
            value.ControlStatementType,
            value.ControlStatementQuantity,
            value.ControlStatementUnit);
    }

    private sealed record ConnectorAttachmentTransport(
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("mimeType")] string MimeType,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("description")] string? Description)
    {
        internal static ConnectorAttachmentTransport From(ConnectorBusinessAttachment value) =>
            new(value.FileName, value.MimeType, value.Content, value.Description);
    }

    private sealed record ConnectorLegacySubmitTransport(
        [property: JsonPropertyName("customerRef")] string? CustomerRef,
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("externalId")] string? ExternalId,
        [property: JsonPropertyName("idempotencyKey")] string? IdempotencyKey,
        [property: JsonPropertyName("payload")] JsonElement? Payload,
        [property: JsonPropertyName("send")] ConnectorLegacySendTransport? Send,
        [property: JsonPropertyName("options")] JsonElement? Options)
    {
        internal static ConnectorLegacySubmitTransport From(
            ConnectorSubmitDocumentRequest request,
            string? customerRef = null) => new(
                customerRef ?? request.CustomerRef,
                request.Mode ?? "stage",
                request.ExternalId,
                request.IdempotencyKey,
                request.Payload is null
                    ? null
                    : JsonSerializer.SerializeToElement(request.Payload, HttpRequestor.JsonOptions),
                request.Send is null
                    ? null
                    : new ConnectorLegacySendTransport(request.Send.Policy, request.Send.SendAt),
                request.Options is null
                    ? null
                    : JsonSerializer.SerializeToElement(request.Options, HttpRequestor.JsonOptions));
    }

    private sealed record ConnectorLegacySendTransport(
        [property: JsonPropertyName("policy")] string Policy,
        [property: JsonPropertyName("sendAt")] string? SendAt);

    /// <summary>Autopilot-stage submit compatibility alias retained without mutating caller input.</summary>
    public Task<ConnectorAutopilotRunResponse> SubmitDocumentAsync(
        ConnectorSubmitDocumentRequest request,
        CancellationToken ct = default)
        => _http.RequestAsync<ConnectorAutopilotRunResponse>(
            HttpMethod.Post,
            "/connector/autopilot",
            ConnectorLegacySubmitTransport.From(request),
            ct,
            omitFirmId: true);

    internal Task<ConnectorAutopilotRunResponse> SubmitCustomerDocumentLegacyAsync(
        string customerRef,
        ConnectorSubmitDocumentRequest request,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(request.CustomerRef) && request.CustomerRef != customerRef)
            throw new ArgumentException("Connector customerRef conflicts with scoped customer.", nameof(request));
        return _http.RequestAsync<ConnectorAutopilotRunResponse>(
            HttpMethod.Post,
            "/connector/autopilot",
            ConnectorLegacySubmitTransport.From(request, customerRef),
            ct,
            omitFirmId: true);
    }

    /// <summary>Validate receiver reachability and payload readiness before sending.</summary>
    public Task<ConnectorPreflightResponse> PreflightAsync(ConnectorPreflightRequest request, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorPreflightResponse>(HttpMethod.Post, "/connector/preflight", request, ct);

    /// <summary>Send an ERP document payload through Connector.</summary>
    public Task<ConnectorSendResponse> SendAsync(ConnectorSendRequest request, CancellationToken ct = default)
        => SendAsync(request, idempotencyKey: null, ct);

    /// <summary>Send an ERP document payload through Connector with an optional Idempotency-Key header.</summary>
    public Task<ConnectorSendResponse> SendAsync(ConnectorSendRequest request, string? idempotencyKey, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorSendResponse>(HttpMethod.Post, "/connector/send", request, idempotencyKey, ct);

    /// <summary>Get Connector status for a document ID.</summary>
    public Task<ConnectorStatusResponse> StatusAsync(string documentId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorStatusResponse>(HttpMethod.Get, $"/connector/status/{Uri.EscapeDataString(documentId)}", ct);

    /// <summary>List Connector inbox documents with cursor pagination.</summary>
    public Task<ConnectorInboxListResponse> InboxAsync(ConnectorListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()));
        return _http.RequestAsync<ConnectorInboxListResponse>(HttpMethod.Get, $"/connector/inbox{qs}", ct);
    }

    /// <summary>Retrieve a single Connector inbox document.</summary>
    public Task<ConnectorInboxDocument> GetInboxDocumentAsync(string documentId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorInboxDocument>(HttpMethod.Get, $"/connector/inbox/{Uri.EscapeDataString(documentId)}", ct);

    /// <summary>Acknowledge a Connector inbox document as processed.</summary>
    public Task<ConnectorAckResponse> AckAsync(string documentId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorAckResponse>(
            HttpMethod.Post,
            $"/connector/inbox/{Uri.EscapeDataString(documentId)}/ack",
            new { },
            ct);

    /// <summary>List Connector polling events with cursor pagination.</summary>
    public Task<ConnectorEventsResponse> EventsAsync(ConnectorListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()));
        return _http.RequestAsync<ConnectorEventsResponse>(HttpMethod.Get, $"/connector/events{qs}", ct);
    }

    internal Task<ConnectorBusinessEventsResponse> ListCustomerEventsAsync(
        string customerRef,
        ConnectorListParams? @params = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerRef))
            throw new ArgumentException("Connector customerRef is required.", nameof(customerRef));
        var qs = HttpRequestor.BuildQuery(
            ("customerRef", customerRef),
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()));
        return _http.RequestAsync<ConnectorBusinessEventsResponse>(HttpMethod.Get, $"/connector/events{qs}", ct, omitFirmId: true);
    }

    /// <summary>Stage one or more ERP invoices without immediate Peppol delivery.</summary>
    public Task<ConnectorOutboxStageResponse> StageOutboxAsync(ConnectorOutboxStageRequest request, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorOutboxStageResponse>(HttpMethod.Post, "/connector/outbox", request, ct);

    /// <summary>List staged Connector outbox items.</summary>
    public Task<ConnectorOutboxListResponse> ListOutboxAsync(ConnectorOutboxListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("status", @params?.Status),
            ("limit", @params?.Limit?.ToString()),
            ("offset", @params?.Offset?.ToString()));
        return _http.RequestAsync<ConnectorOutboxListResponse>(HttpMethod.Get, $"/connector/outbox{qs}", ct);
    }

    /// <summary>Retrieve a single Connector outbox item.</summary>
    public Task<ConnectorOutboxItem> GetOutboxItemAsync(string outboxId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorOutboxItem>(HttpMethod.Get, $"/connector/outbox/{Uri.EscapeDataString(outboxId)}", ct);

    /// <summary>Send one staged outbox item through the Connector workflow.</summary>
    public Task<ConnectorOutboxItem> SendOutboxItemAsync(string outboxId, ConnectorOutboxSendOptions? options = null, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorOutboxItem>(
            HttpMethod.Post,
            $"/connector/outbox/{Uri.EscapeDataString(outboxId)}/send",
            options ?? new ConnectorOutboxSendOptions(),
            ct);

    /// <summary>Send ready, failed, or due scheduled outbox items in a batch.</summary>
    public Task<ConnectorOutboxBatchSendResponse> SendOutboxBatchAsync(ConnectorOutboxBatchSendRequest? request = null, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorOutboxBatchSendResponse>(
            HttpMethod.Post,
            "/connector/outbox/send",
            request ?? new ConnectorOutboxBatchSendRequest(),
            ct);

    /// <summary>Cancel a staged outbox item before it is sent.</summary>
    public Task<ConnectorOutboxItem> CancelOutboxItemAsync(string outboxId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorOutboxItem>(HttpMethod.Delete, $"/connector/outbox/{Uri.EscapeDataString(outboxId)}", ct);

    /// <summary>Start a managed Connector Autopilot lifecycle run.</summary>
    public Task<ConnectorAutopilotRunResponse> AutopilotAsync(ConnectorAutopilotRequest request, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorAutopilotRunResponse>(HttpMethod.Post, "/connector/autopilot", request, ct, omitFirmId: true);

    /// <summary>Map a saved Connector Mapper template input into preview, stage, or send.</summary>
    public Task<Dictionary<string, object?>> MapperAsync(ConnectorMapperRequest request, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(HttpMethod.Post, "/connector/mapper", request, ct, omitFirmId: true);

    /// <summary>Normalize a loose ERP/customer payload into a Connector lifecycle run.</summary>
    public Task<ConnectorAutopilotRunResponse> ZenInputAsync(ConnectorZenInputRequest request, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorAutopilotRunResponse>(HttpMethod.Post, "/connector/zen-input", request, ct, omitFirmId: true);

    /// <summary>Retrieve an Autopilot run by ID.</summary>
    public Task<ConnectorAutopilotRunResponse> GetAutopilotRunAsync(string autopilotId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorAutopilotRunResponse>(HttpMethod.Get, $"/connector/autopilot/{Uri.EscapeDataString(autopilotId)}", ct, omitFirmId: true);

    /// <summary>Send a shadow-validated or staged Autopilot run.</summary>
    public Task<ConnectorAutopilotRunResponse> SendAutopilotRunAsync(string autopilotId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorAutopilotRunResponse>(
            HttpMethod.Post,
            $"/connector/autopilot/{Uri.EscapeDataString(autopilotId)}/send",
            new { },
            ct,
            omitFirmId: true);

    /// <summary>List Connector reconciliation items for ERP state sync.</summary>
    public Task<ConnectorReconcileResponse> ReconcileAsync(ConnectorReconcileParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("status", @params?.Status),
            ("since", @params?.Since));
        return _http.RequestAsync<ConnectorReconcileResponse>(HttpMethod.Get, $"/connector/reconcile{qs}", ct, omitFirmId: true);
    }

    /// <summary>List Connector-managed customer mailboxes.</summary>
    public Task<ConnectorMailboxListResponse> MailboxesAsync(CancellationToken ct = default)
        => _http.RequestAsync<ConnectorMailboxListResponse>(HttpMethod.Get, "/connector/mailbox", ct, omitFirmId: true);

    /// <summary>Repair Connector mailbox state for one customer or all customers.</summary>
    public Task<Dictionary<string, object?>> RepairMailboxAsync(ConnectorMailboxRepairRequest? request = null, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(HttpMethod.Post, "/connector/mailbox/repair", request ?? new ConnectorMailboxRepairRequest(), ct, omitFirmId: true);

    /// <summary>Update the managed send policy for a Connector mailbox.</summary>
    public Task<ConnectorMailboxUpdateResponse> UpdateMailboxSendPolicyAsync(string customerRef, ConnectorSendPolicyOptions request, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorMailboxUpdateResponse>(
            HttpMethod.Patch,
            $"/connector/mailbox/{Uri.EscapeDataString(customerRef)}/send-policy",
            request,
            ct,
            omitFirmId: true);

    /// <summary>List Connector sync items for ERP reconciliation cursors.</summary>
    public Task<ConnectorSyncResponse> SyncAsync(ConnectorSyncParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("customerRef", @params?.CustomerRef),
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()));
        return _http.RequestAsync<ConnectorSyncResponse>(HttpMethod.Get, $"/connector/sync{qs}", ct, omitFirmId: true);
    }

    /// <summary>Retrieve a Connector document lifecycle snapshot.</summary>
    public Task<Dictionary<string, object?>> GetDocumentAsync(string documentId, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Get,
            $"/connector/documents/{Uri.EscapeDataString(documentId)}",
            ct,
            omitFirmId: true);

    internal Task<Dictionary<string, object?>> GetCustomerDocumentAsync(
        string documentId,
        string customerRef,
        CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Get,
            $"/connector/documents/{Uri.EscapeDataString(documentId)}{CustomerRefQuery(customerRef)}",
            ct,
            omitFirmId: true);

    internal Task<ConnectorBusinessDocument> GetBusinessDocumentAsync(string documentId, string customerRef, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorBusinessDocument>(
            HttpMethod.Get,
            $"/connector/documents/{Uri.EscapeDataString(documentId)}{CustomerRefQuery(customerRef)}",
            ct,
            omitFirmId: true);

    internal Task<ConnectorBusinessDocumentListResponse> ListCustomerDocumentsAsync(
        string customerRef,
        ConnectorBusinessDocumentListParams? @params = null,
        CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("customerRef", customerRef),
            ("direction", @params?.Direction),
            ("state", @params?.State),
            ("type", @params?.Type),
            ("createdAfter", @params?.CreatedAfter),
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()));
        return _http.RequestAsync<ConnectorBusinessDocumentListResponse>(HttpMethod.Get, $"/connector/documents{qs}", ct, omitFirmId: true);
    }

    internal Task<ConnectorBusinessAcknowledgeResponse> AcknowledgeDocumentAsync(
        string documentId,
        string reference,
        string customerRef,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Connector reference is required.", nameof(reference));
        return _http.RequestIdempotentAsync<ConnectorBusinessAcknowledgeResponse>(
            HttpMethod.Post,
            $"/connector/documents/{Uri.EscapeDataString(documentId)}/acknowledge{CustomerRefQuery(customerRef)}",
            new { reference },
            ct,
            omitFirmId: true);
    }

    internal Task<ConnectorInvoiceResponseResult> RespondDocumentAsync(
        string documentId,
        string customerRef,
        ConnectorInvoiceResponseRequest request,
        CancellationToken ct = default)
    {
        var normalizedDocumentId = documentId?.Trim() ?? "";
        var normalizedCustomerRef = TrimString(customerRef ?? "");
        if (normalizedDocumentId.Length == 0)
            throw new ArgumentException("Connector documentId is required.", nameof(documentId));
        if (normalizedCustomerRef.Length == 0)
            throw new ArgumentException("Connector customerRef is required.", nameof(customerRef));
        if (!InvoiceResponseStatuses.Contains(request.Status))
            throw new ArgumentException("Invalid Connector response status.", nameof(request));
        return _http.RequestIdempotentAsync<ConnectorInvoiceResponseResult>(
            HttpMethod.Post,
            $"/connector/documents/{Uri.EscapeDataString(normalizedDocumentId)}/respond{CustomerRefQuery(normalizedCustomerRef)}",
            request,
            ct,
            omitFirmId: true);
    }

    internal Task<ConnectorBusinessDocument> SendDocumentAsync(string documentId, string customerRef, CancellationToken ct = default)
        => TransitionDocumentAsync(documentId, "send", customerRef, ct);

    internal Task<ConnectorBusinessDocument> CancelDocumentAsync(string documentId, string customerRef, CancellationToken ct = default)
        => TransitionDocumentAsync(documentId, "cancel", customerRef, ct);

    private Task<ConnectorBusinessDocument> TransitionDocumentAsync(
        string documentId,
        string action,
        string customerRef,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("Connector documentId is required.", nameof(documentId));
        return _http.RequestIdempotentAsync<ConnectorBusinessDocument>(
            HttpMethod.Post,
            $"/connector/documents/{Uri.EscapeDataString(documentId)}/{action}{CustomerRefQuery(customerRef)}",
            ct,
            omitFirmId: true);
    }

    /// <summary>Download a Connector document UBL XML body.</summary>
    public Task<string> GetDocumentUblAsync(string documentId, CancellationToken ct = default)
        => GetDocumentUblAsync(documentId, null, ct);

    internal Task<string> GetDocumentUblAsync(string documentId, string? customerRef, CancellationToken ct = default)
        => _http.RequestStringAsync(
            HttpMethod.Get,
            $"/connector/documents/{Uri.EscapeDataString(documentId)}/ubl{CustomerRefQuery(customerRef)}",
            ct,
            omitFirmId: true);

    /// <summary>Retrieve Connector document delivery evidence.</summary>
    public Task<Dictionary<string, object?>> GetDocumentEvidenceAsync(string documentId, CancellationToken ct = default)
        => GetDocumentEvidenceAsync(documentId, null, ct);

    internal Task<Dictionary<string, object?>> GetDocumentEvidenceAsync(string documentId, string? customerRef, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Get,
            $"/connector/documents/{Uri.EscapeDataString(documentId)}/evidence{CustomerRefQuery(customerRef)}",
            ct,
            omitFirmId: true);

    /// <summary>Retrieve the Connector evidence bundle manifest.</summary>
    public Task<Dictionary<string, object?>> GetDocumentEvidenceBundleAsync(string documentId, CancellationToken ct = default)
        => GetDocumentEvidenceBundleAsync(documentId, null, ct);

    internal Task<Dictionary<string, object?>> GetDocumentEvidenceBundleAsync(string documentId, string? customerRef, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Get,
            $"/connector/documents/{Uri.EscapeDataString(documentId)}/evidence-bundle{CustomerRefQuery(customerRef)}",
            ct,
            omitFirmId: true);

    /// <summary>Retrieve the Connector support packet manifest.</summary>
    public Task<Dictionary<string, object?>> GetDocumentSupportPacketAsync(string documentId, CancellationToken ct = default)
        => GetDocumentSupportPacketAsync(documentId, null, ct);

    internal Task<Dictionary<string, object?>> GetDocumentSupportPacketAsync(string documentId, string? customerRef, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Get,
            $"/connector/documents/{Uri.EscapeDataString(documentId)}/support-packet{CustomerRefQuery(customerRef)}",
            ct,
            omitFirmId: true);

    private static string CustomerRefQuery(string? customerRef)
        => HttpRequestor.BuildQuery(("customerRef", customerRef));

    /// <summary>Execute a pending Connector action.</summary>
    public Task<ConnectorActionResponse> RunActionAsync(string actionId, ConnectorActionRequest? request = null, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorActionResponse>(
            HttpMethod.Post,
            $"/connector/actions/{Uri.EscapeDataString(actionId)}",
            request ?? new ConnectorActionRequest(),
            ct,
            omitFirmId: true);
}

public sealed class ConnectorCustomersResource
{
    private readonly ConnectorResource _connector;

    internal ConnectorCustomersResource(ConnectorResource connector) => _connector = connector;

    public ConnectorCustomerResource For(string customerRef)
    {
        var normalizedCustomerRef = ConnectorResource.TrimString(customerRef ?? "");
        if (normalizedCustomerRef.Length == 0)
            throw new ArgumentException("Connector customerRef is required.", nameof(customerRef));
        return new ConnectorCustomerResource(_connector, normalizedCustomerRef);
    }
}

public sealed class ConnectorCustomerResource
{
    private readonly ConnectorResource _connector;
    private readonly string _customerRef;

    internal ConnectorCustomerResource(ConnectorResource connector, string customerRef)
    {
        _connector = connector;
        _customerRef = customerRef;
        Documents = new ConnectorCustomerDocumentsResource(connector, customerRef);
        Events = new ConnectorCustomerEventsResource(connector, customerRef);
        Advanced = new ConnectorCustomerAdvancedResource(this, connector, customerRef);
        Mailbox = Advanced.Mailbox;
    }

    public ConnectorCustomerDocumentsResource Documents { get; }
    public ConnectorCustomerEventsResource Events { get; }
    public ConnectorCustomerAdvancedResource Advanced { get; }
    public ConnectorCustomerMailboxResource Mailbox { get; }

    public Task<ConnectorAutopilotRunResponse> SubmitDocumentAsync(
        ConnectorSubmitDocumentRequest request,
        CancellationToken ct = default)
        => _connector.SubmitCustomerDocumentLegacyAsync(_customerRef, request, ct);

    public Task<ConnectorAutopilotRunResponse> AutopilotAsync(ConnectorAutopilotRequest request, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(request.CustomerRef) && request.CustomerRef != _customerRef)
            throw new ArgumentException("Connector customerRef conflicts with scoped customer.", nameof(request));
        var scoped = new ConnectorAutopilotRequest
        {
            CustomerRef = _customerRef,
            Mode = request.Mode,
            ExternalId = request.ExternalId,
            IdempotencyKey = request.IdempotencyKey,
            Payload = new Dictionary<string, object?>(request.Payload),
            Send = request.Send is null
                ? null
                : new ConnectorSendPolicyOptions { Policy = request.Send.Policy, SendAt = request.Send.SendAt },
            Options = request.Options is null ? null : new Dictionary<string, object?>(request.Options),
        };
        return _connector.Advanced.AutopilotAsync(scoped, ct);
    }

    public Task<Dictionary<string, object?>> MapperAsync(ConnectorMapperRequest request, CancellationToken ct = default)
    {
        if (request.Execute is not null && request.Execute != "preview")
            throw new ArgumentException("Connector Mapper only supports preview normalization.", nameof(request));
        if (!string.IsNullOrEmpty(request.CustomerRef) && request.CustomerRef != _customerRef)
            throw new ArgumentException("Connector customerRef conflicts with scoped customer.", nameof(request));
        var scoped = new ConnectorMapperRequest
        {
            TemplateKey = request.TemplateKey,
            SourceType = request.SourceType,
            SourceText = request.SourceText,
            SourceJson = request.SourceJson is null ? null : new Dictionary<string, object?>(request.SourceJson),
            CustomerRef = _customerRef,
            Execute = "preview",
            Confirmed = request.Confirmed,
            FieldMap = request.FieldMap is null ? null : new Dictionary<string, object?>(request.FieldMap),
            Defaults = request.Defaults is null ? null : new Dictionary<string, object?>(request.Defaults),
            Extra = new Dictionary<string, object?>(request.Extra),
        };
        return _connector.Advanced.MapperAsync(scoped, ct);
    }

    public Task<ConnectorAutopilotRunResponse> ZenInputAsync(ConnectorZenInputRequest request, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(request.CustomerRef) && request.CustomerRef != _customerRef)
            throw new ArgumentException("Connector customerRef conflicts with scoped customer.", nameof(request));
        var scoped = new ConnectorZenInputRequest
        {
            CustomerRef = _customerRef,
            PreviewOnly = request.PreviewOnly,
            Mode = request.Mode,
            ExternalId = request.ExternalId,
            IdempotencyKey = request.IdempotencyKey,
            InvoiceNo = request.InvoiceNo,
            InvoiceNumber = request.InvoiceNumber,
            ReceiverPeppolId = request.ReceiverPeppolId,
            Receiver = request.Receiver is null ? null : new Dictionary<string, object?>(request.Receiver),
            Buyer = request.Buyer is null ? null : new Dictionary<string, object?>(request.Buyer),
            Customer = request.Customer is null ? null : new Dictionary<string, object?>(request.Customer),
            Lines = request.Lines?.Select(line => new Dictionary<string, object?>(line)).ToList(),
            Items = request.Items?.Select(item => new Dictionary<string, object?>(item)).ToList(),
            Send = request.Send is null
                ? null
                : new ConnectorSendPolicyOptions { Policy = request.Send.Policy, SendAt = request.Send.SendAt },
            Extra = new Dictionary<string, object?>(request.Extra),
        };
        return _connector.Advanced.ZenInputAsync(scoped, ct);
    }

    public Task<ConnectorSyncResponse> SyncAsync(ConnectorSyncParams? parameters = null, CancellationToken ct = default)
    {
        parameters ??= new ConnectorSyncParams();
        if (!string.IsNullOrEmpty(parameters.CustomerRef) && parameters.CustomerRef != _customerRef)
            throw new ArgumentException("Connector customerRef conflicts with scoped customer.", nameof(parameters));
        var scoped = new ConnectorSyncParams
        {
            CustomerRef = _customerRef,
            Cursor = parameters.Cursor,
            Limit = parameters.Limit,
        };
        return _connector.Advanced.SyncAsync(scoped, ct);
    }

}

/// <summary>Direct Connector document facade kept for source compatibility.</summary>
public class ConnectorDocumentsResource
{
    protected readonly ConnectorResource Connector;
    private readonly string? _customerRef;

    internal ConnectorDocumentsResource(ConnectorResource connector, string? customerRef = null)
    {
        Connector = connector;
        _customerRef = customerRef;
    }

    public Task<Dictionary<string, object?>> GetAsync(string documentId, CancellationToken ct = default)
        => _customerRef is null
            ? Connector.GetDocumentAsync(documentId, ct)
            : Connector.GetCustomerDocumentAsync(documentId, _customerRef, ct);

    public virtual Task<ConnectorInvoiceResponseResult> RespondAsync(
        string documentId,
        string customerRef,
        ConnectorInvoiceResponseRequest request,
        CancellationToken ct = default)
        => Connector.RespondDocumentAsync(documentId, customerRef, request, ct);

    public Task<string> UblAsync(string documentId, CancellationToken ct = default)
        => Connector.GetDocumentUblAsync(documentId, _customerRef, ct);

    public Task<Dictionary<string, object?>> EvidenceAsync(string documentId, CancellationToken ct = default)
        => Connector.GetDocumentEvidenceAsync(documentId, _customerRef, ct);

    public Task<Dictionary<string, object?>> EvidenceBundleAsync(string documentId, CancellationToken ct = default)
        => Connector.GetDocumentEvidenceBundleAsync(documentId, _customerRef, ct);

    public Task<Dictionary<string, object?>> SupportPacketAsync(string documentId, CancellationToken ct = default)
        => Connector.GetDocumentSupportPacketAsync(documentId, _customerRef, ct);
}

public sealed class ConnectorCustomerDocumentsResource : ConnectorDocumentsResource
{
    private readonly string _customerRef;

    internal ConnectorCustomerDocumentsResource(ConnectorResource connector, string customerRef) : base(connector, customerRef)
    {
        _customerRef = customerRef;
    }

    public Task<ConnectorBusinessDocument> SendAsync(ConnectorBusinessDocumentRequest request, string? idempotencyKey = null, CancellationToken ct = default)
        => Connector.SubmitCustomerDocumentAsync(_customerRef, request, "send", idempotencyKey, ct);

    public Task<ConnectorBusinessDocument> StageAsync(ConnectorBusinessDocumentRequest request, string? idempotencyKey = null, CancellationToken ct = default)
        => Connector.SubmitCustomerDocumentAsync(_customerRef, request, "stage", idempotencyKey, ct);

    public Task<ConnectorBusinessDocumentListResponse> ListAsync(ConnectorBusinessDocumentListParams? @params = null, CancellationToken ct = default)
        => Connector.ListCustomerDocumentsAsync(_customerRef, @params, ct);

    /// <summary>Typed business-document detail for new Connector integrations.</summary>
    public Task<ConnectorBusinessDocument> GetBusinessDocumentAsync(string documentId, CancellationToken ct = default)
        => Connector.GetBusinessDocumentAsync(documentId, _customerRef, ct);

    public Task<ConnectorInvoiceResponseResult> RespondAsync(
        string documentId,
        ConnectorInvoiceResponseRequest request,
        CancellationToken ct = default)
        => Connector.RespondDocumentAsync(documentId, _customerRef, request, ct);

    public override Task<ConnectorInvoiceResponseResult> RespondAsync(
        string documentId,
        string customerRef,
        ConnectorInvoiceResponseRequest request,
        CancellationToken ct = default)
    {
        if (!string.Equals(ConnectorResource.TrimString(customerRef ?? ""), _customerRef, StringComparison.Ordinal))
            throw new ArgumentException("Connector customerRef conflicts with scoped customer.", nameof(customerRef));
        return Connector.RespondDocumentAsync(documentId, _customerRef, request, ct);
    }

    public Task<ConnectorBusinessAcknowledgeResponse> AcknowledgeAsync(string documentId, string reference, CancellationToken ct = default)
        => Connector.AcknowledgeDocumentAsync(documentId, reference, _customerRef, ct);

    /// <summary>Send a previously staged business document.</summary>
    public Task<ConnectorBusinessDocument> SendDocumentAsync(string documentId, CancellationToken ct = default)
        => Connector.SendDocumentAsync(documentId, _customerRef, ct);

    /// <summary>Cancel a staged business document before delivery.</summary>
    public Task<ConnectorBusinessDocument> CancelDocumentAsync(string documentId, CancellationToken ct = default)
        => Connector.CancelDocumentAsync(documentId, _customerRef, ct);

}

/// <summary>Advanced Connector document artifacts such as UBL and delivery evidence.</summary>
public sealed class ConnectorAdvancedDocumentsResource
{
    private readonly ConnectorResource _connector;
    private readonly string? _customerRef;

    internal ConnectorAdvancedDocumentsResource(ConnectorResource connector, string? customerRef = null)
    {
        _connector = connector;
        _customerRef = customerRef;
    }

    public Task<string> UblAsync(string documentId, CancellationToken ct = default)
        => _connector.GetDocumentUblAsync(documentId, _customerRef, ct);

    public Task<Dictionary<string, object?>> EvidenceAsync(string documentId, CancellationToken ct = default)
        => _connector.GetDocumentEvidenceAsync(documentId, _customerRef, ct);

    public Task<Dictionary<string, object?>> EvidenceBundleAsync(string documentId, CancellationToken ct = default)
        => _connector.GetDocumentEvidenceBundleAsync(documentId, _customerRef, ct);

    public Task<Dictionary<string, object?>> SupportPacketAsync(string documentId, CancellationToken ct = default)
        => _connector.GetDocumentSupportPacketAsync(documentId, _customerRef, ct);
}

public sealed class ConnectorCustomerAdvancedResource
{
    private readonly ConnectorCustomerResource _customer;

    internal ConnectorCustomerAdvancedResource(
        ConnectorCustomerResource customer,
        ConnectorResource connector,
        string customerRef)
    {
        _customer = customer;
        Documents = new ConnectorAdvancedDocumentsResource(connector, customerRef);
        Mailbox = new ConnectorCustomerMailboxResource(connector, customerRef);
    }

    public ConnectorAdvancedDocumentsResource Documents { get; }
    public ConnectorCustomerMailboxResource Mailbox { get; }

    public Task<ConnectorAutopilotRunResponse> AutopilotAsync(ConnectorAutopilotRequest request, CancellationToken ct = default)
        => _customer.AutopilotAsync(request, ct);

    /// <summary>Preview and normalize source data without staging or sending.</summary>
    public Task<Dictionary<string, object?>> MapperAsync(ConnectorMapperRequest request, CancellationToken ct = default)
        => _customer.MapperAsync(request, ct);

    public Task<ConnectorAutopilotRunResponse> ZenInputAsync(ConnectorZenInputRequest request, CancellationToken ct = default)
        => _customer.ZenInputAsync(request, ct);

    public Task<ConnectorSyncResponse> SyncAsync(ConnectorSyncParams? parameters = null, CancellationToken ct = default)
        => _customer.SyncAsync(parameters, ct);
}

public sealed class ConnectorCustomerEventsResource
{
    private readonly ConnectorResource _connector;
    private readonly string _customerRef;

    internal ConnectorCustomerEventsResource(ConnectorResource connector, string customerRef)
    {
        _connector = connector;
        _customerRef = customerRef;
    }

    public Task<ConnectorBusinessEventsResponse> ListAsync(ConnectorListParams? @params = null, CancellationToken ct = default)
    {
        return _connector.ListCustomerEventsAsync(_customerRef, @params, ct);
    }
}

public sealed class ConnectorCustomerMailboxResource
{
    private readonly ConnectorResource _connector;
    private readonly string _customerRef;

    internal ConnectorCustomerMailboxResource(ConnectorResource connector, string customerRef)
    {
        _connector = connector;
        _customerRef = customerRef;
    }

    public Task<Dictionary<string, object?>> RepairAsync(CancellationToken ct = default)
        => _connector.Advanced.RepairMailboxAsync(new ConnectorMailboxRepairRequest { CustomerRef = _customerRef }, ct);

    public Task<ConnectorMailboxUpdateResponse> UpdateSendPolicyAsync(ConnectorSendPolicyOptions request, CancellationToken ct = default)
        => _connector.Advanced.UpdateMailboxSendPolicyAsync(_customerRef, request, ct);
}
