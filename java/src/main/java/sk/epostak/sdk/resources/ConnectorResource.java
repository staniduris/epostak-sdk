package sk.epostak.sdk.resources;

import com.google.gson.reflect.TypeToken;
import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.*;

import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.HexFormat;
import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Customer-scoped Connector documents and events for ERP teams.
 * <p>
 * Start from {@link #customers()} after ePošťák approves the firm and the
 * integrator stores its own stable customer reference in the dashboard.
 * Lower-level compatibility and orchestration APIs are grouped under
 * {@link #advanced()}.
 */
public final class ConnectorResource {
    private static final String TRIM_STRING_CHARS =
            "\\x{0009}-\\x{000D}\\x{0020}\\x{00A0}\\x{1680}\\x{2000}-\\x{200A}" +
            "\\x{2028}\\x{2029}\\x{202F}\\x{205F}\\x{3000}\\x{FEFF}";

    static String trimString(String value) {
        return value.replaceAll("^[" + TRIM_STRING_CHARS + "]+|[" + TRIM_STRING_CHARS + "]+$", "");
    }

    private final HttpClient http;
    private final ConnectorDocumentsResource documents;
    private final ConnectorCustomersResource customers;
    private final ConnectorAdvancedResource advanced;
    private final ConnectorWebhookResource webhook;

    /**
     * Creates a new Connector resource.
     *
     * @param http the HTTP client used for API communication
     */
    public ConnectorResource(HttpClient http) {
        this.http = http;
        this.documents = new ConnectorDocumentsResource(this);
        this.customers = new ConnectorCustomersResource(this);
        this.advanced = new ConnectorAdvancedResource(this);
        this.webhook = new ConnectorWebhookResource(http);
    }

    public ConnectorDocumentsResource documents() {
        return documents;
    }

    public ConnectorCustomersResource customers() {
        return customers;
    }

    /**
     * Advanced and compatibility Connector workflows.
     * <p>
     * New ERP integrations should start from {@link #customers()} and use the
     * customer-scoped documents and events resources. This surface keeps the
     * lower-level preflight, outbox, Autopilot, mapper, mailbox, and sync APIs
     * available when an integration explicitly needs them.
     *
     * @return advanced Connector operations
     */
    public ConnectorAdvancedResource advanced() {
        return advanced;
    }

    /** Single global push webhook shared by all managed Connector firms. */
    public ConnectorWebhookResource webhook() {
        return webhook;
    }

    ConnectorBusinessDocument submitCustomerDocument(
            String customerRef,
            ConnectorBusinessDocumentRequest request,
            String delivery,
            String idempotencyKey
    ) {
        if (customerRef == null || request.externalId() == null) {
            throw new IllegalArgumentException("Connector externalId is required");
        }
        String normalizedCustomerRef = trimString(customerRef);
        String normalizedExternalId = trimString(request.externalId());
        if (normalizedCustomerRef.isEmpty()) {
            throw new IllegalArgumentException("Connector customerRef is required");
        }
        if (normalizedExternalId.isEmpty()) {
            throw new IllegalArgumentException("Connector externalId is required");
        }
        String key = idempotencyKey != null
                ? validateIdempotencyKey(idempotencyKey)
                : defaultIdempotencyKey(normalizedCustomerRef, normalizedExternalId);
        return http.postIdempotentNoFirm(
                "/connector/documents",
                request.toMap(normalizedCustomerRef, delivery, normalizedExternalId),
                ConnectorBusinessDocument.class,
                key
        );
    }

    /**
     * Autopilot-stage submit compatibility alias retained with its original
     * request and response semantics.
     */
    public ConnectorAutopilotRunResponse submitDocument(ConnectorSubmitDocumentRequest request) {
        Map<String, Object> body = new LinkedHashMap<>(request.toMap());
        if (request.mode() == null) body.put("mode", "stage");
        return http.postNoFirm("/connector/autopilot", body, ConnectorAutopilotRunResponse.class);
    }

    private static String defaultIdempotencyKey(String customerRef, String externalId) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            updateLengthPrefixed(digest, trimString(customerRef));
            updateLengthPrefixed(digest, trimString(externalId));
            return "connector:v1:" + HexFormat.of().formatHex(digest.digest());
        } catch (NoSuchAlgorithmException error) {
            throw new IllegalStateException("SHA-256 unavailable", error);
        }
    }

    private static String validateIdempotencyKey(String value) {
        int byteLength = value.getBytes(StandardCharsets.UTF_8).length;
        if (trimString(value).isEmpty() || byteLength > 255) {
            throw new IllegalArgumentException("Connector idempotency key must be 1-255 UTF-8 bytes");
        }
        return value;
    }

    private static void updateLengthPrefixed(MessageDigest digest, String value) {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        digest.update(ByteBuffer.allocate(Integer.BYTES).putInt(bytes.length).array());
        digest.update(bytes);
    }

    /**
     * Validate receiver reachability and payload readiness before sending.
     *
     * @param request Connector preflight payload
     * @return repair report and readiness response
     */
    public ConnectorPreflightResponse preflight(ConnectorPreflightRequest request) {
        return http.post("/connector/preflight", request, ConnectorPreflightResponse.class);
    }

    /**
     * Send an ERP document payload through Connector.
     *
     * @param request arbitrary Connector send payload
     * @return send response with documentId and status
     */
    public ConnectorSendResponse send(Map<String, Object> request) {
        return http.post("/connector/send", request, ConnectorSendResponse.class);
    }

    /**
     * Send an ERP document payload through Connector with an Idempotency-Key header.
     *
     * @param request arbitrary Connector send payload
     * @param idempotencyKey optional idempotency key, or {@code null}
     * @return send response with documentId and status
     */
    public ConnectorSendResponse send(Map<String, Object> request, String idempotencyKey) {
        return http.postIdempotent("/connector/send", request, ConnectorSendResponse.class, idempotencyKey);
    }

    /**
     * Get Connector status for a document ID.
     *
     * @param documentId document UUID
     * @return Connector status response
     */
    public ConnectorStatusResponse status(String documentId) {
        return http.get("/connector/status/" + HttpClient.encode(documentId), ConnectorStatusResponse.class);
    }

    /**
     * List Connector inbox documents with cursor pagination.
     *
     * @param params optional cursor pagination params
     * @return Connector inbox page
     */
    public ConnectorInboxListResponse inbox(ConnectorListParams params) {
        Map<String, Object> qp = new LinkedHashMap<>();
        if (params != null) {
            qp.put("cursor", params.cursor());
            qp.put("limit", params.limit());
        }
        return http.get("/connector/inbox" + HttpClient.buildQuery(qp), ConnectorInboxListResponse.class);
    }

    /**
     * List Connector inbox documents with default parameters.
     *
     * @return first Connector inbox page
     */
    public ConnectorInboxListResponse inbox() {
        return inbox(ConnectorListParams.empty());
    }

    /**
     * Retrieve a single Connector inbox document.
     *
     * @param documentId document UUID
     * @return Connector inbox document
     */
    public ConnectorInboxDocument getInboxDocument(String documentId) {
        return http.get("/connector/inbox/" + HttpClient.encode(documentId), ConnectorInboxDocument.class);
    }

    /**
     * Acknowledge a Connector inbox document as processed.
     *
     * @param documentId document UUID
     * @return Connector ack response
     */
    public ConnectorAckResponse ack(String documentId) {
        return http.post(
                "/connector/inbox/" + HttpClient.encode(documentId) + "/ack",
                Map.of(),
                ConnectorAckResponse.class
        );
    }

    /**
     * List Connector polling events with cursor pagination.
     *
     * @param params optional cursor pagination params
     * @return Connector events page
     */
    public ConnectorEventsResponse events(ConnectorListParams params) {
        Map<String, Object> qp = new LinkedHashMap<>();
        if (params != null) {
            qp.put("cursor", params.cursor());
            qp.put("limit", params.limit());
        }
        return http.get("/connector/events" + HttpClient.buildQuery(qp), ConnectorEventsResponse.class);
    }

    /** Compatibility firm-scoped technical event feed with default params. */
    public ConnectorEventsResponse events() {
        return events(ConnectorListParams.empty());
    }

    /** Compatibility alias for the customer-scoped business event feed. */
    public ConnectorBusinessEventsResponse events(String customerRef, ConnectorListParams params) {
        return listCustomerEvents(customerRef, params);
    }

    ConnectorBusinessEventsResponse listCustomerEvents(String customerRef, ConnectorListParams params) {
        if (customerRef == null || customerRef.isBlank()) {
            throw new IllegalArgumentException("Connector customerRef is required");
        }
        Map<String, Object> qp = new LinkedHashMap<>();
        qp.put("customerRef", customerRef);
        if (params != null) {
            qp.put("cursor", params.cursor());
            qp.put("limit", params.limit());
        }
        return http.getNoFirm("/connector/events" + HttpClient.buildQuery(qp), ConnectorBusinessEventsResponse.class);
    }

    /**
     * Stage one or more ERP invoices without immediate Peppol delivery.
     *
     * @param request Connector outbox staging payload
     * @return stage response with outbox items and repair reports
     */
    public ConnectorOutboxStageResponse stageOutbox(ConnectorOutboxStageRequest request) {
        return http.post("/connector/outbox", request, ConnectorOutboxStageResponse.class);
    }

    /**
     * List staged Connector outbox items.
     *
     * @param params optional status/limit/offset params
     * @return Connector outbox list page
     */
    public ConnectorOutboxListResponse listOutbox(ConnectorOutboxListParams params) {
        Map<String, Object> qp = new LinkedHashMap<>();
        if (params != null) {
            qp.put("status", params.status());
            qp.put("limit", params.limit());
            qp.put("offset", params.offset());
        }
        return http.get("/connector/outbox" + HttpClient.buildQuery(qp), ConnectorOutboxListResponse.class);
    }

    /**
     * List staged Connector outbox items with default params.
     *
     * @return Connector outbox list page
     */
    public ConnectorOutboxListResponse listOutbox() {
        return listOutbox(ConnectorOutboxListParams.empty());
    }

    /**
     * Retrieve a single Connector outbox item.
     *
     * @param outboxId Connector outbox item ID
     * @return Connector outbox item
     */
    public ConnectorOutboxItem getOutboxItem(String outboxId) {
        return http.get("/connector/outbox/" + HttpClient.encode(outboxId), ConnectorOutboxItem.class);
    }

    /**
     * Send one staged outbox item through the Connector workflow.
     *
     * @param outboxId Connector outbox item ID
     * @param options optional send options
     * @return Connector outbox item
     */
    public ConnectorOutboxItem sendOutboxItem(String outboxId, ConnectorOutboxSendOptions options) {
        return http.post(
                "/connector/outbox/" + HttpClient.encode(outboxId) + "/send",
                options == null ? ConnectorOutboxSendOptions.empty() : options,
                ConnectorOutboxItem.class
        );
    }

    /**
     * Send one staged outbox item with default options.
     *
     * @param outboxId Connector outbox item ID
     * @return Connector outbox item
     */
    public ConnectorOutboxItem sendOutboxItem(String outboxId) {
        return sendOutboxItem(outboxId, ConnectorOutboxSendOptions.empty());
    }

    /**
     * Send ready, failed, or due scheduled outbox items in a batch.
     *
     * @param request optional batch send request
     * @return per-item batch send result
     */
    public ConnectorOutboxBatchSendResponse sendOutboxBatch(ConnectorOutboxBatchSendRequest request) {
        return http.post(
                "/connector/outbox/send",
                request == null ? ConnectorOutboxBatchSendRequest.empty() : request,
                ConnectorOutboxBatchSendResponse.class
        );
    }

    /**
     * Send ready, failed, or due scheduled outbox items in a batch.
     *
     * @return per-item batch send result
     */
    public ConnectorOutboxBatchSendResponse sendOutboxBatch() {
        return sendOutboxBatch(ConnectorOutboxBatchSendRequest.empty());
    }

    /**
     * Cancel a staged outbox item before it is sent.
     *
     * @param outboxId Connector outbox item ID
     * @return cancelled Connector outbox item
     */
    public ConnectorOutboxItem cancelOutboxItem(String outboxId) {
        return http.delete("/connector/outbox/" + HttpClient.encode(outboxId), ConnectorOutboxItem.class);
    }

    /**
     * Start a managed Connector Autopilot lifecycle run.
     *
     * @param request Autopilot request with mode, payload, and optional IDs
     * @return Autopilot run lifecycle response
     */
    public ConnectorAutopilotRunResponse autopilot(ConnectorAutopilotRequest request) {
        return http.postNoFirm("/connector/autopilot", request, ConnectorAutopilotRunResponse.class);
    }

    /**
     * Map a saved Connector Mapper template input into preview, stage, or send.
     *
     * @param request Mapper request with templateKey and source payload
     * @return mapping preview, checklist, or Autopilot result
     */
    @SuppressWarnings("unchecked")
    public Map<String, Object> mapper(Map<String, Object> request) {
        return (Map<String, Object>) http.postNoFirm("/connector/mapper", request, Map.class);
    }

    /**
     * Normalize a loose ERP/customer payload into a Connector lifecycle run.
     *
     * @param request Zen input request with customerRef and invoice/customer fields
     * @return Autopilot run lifecycle response
     */
    public ConnectorAutopilotRunResponse zenInput(Map<String, Object> request) {
        return http.postNoFirm("/connector/zen-input", request, ConnectorAutopilotRunResponse.class);
    }

    /**
     * Retrieve an Autopilot run by ID.
     *
     * @param autopilotId Connector Autopilot run ID
     * @return Autopilot run lifecycle response
     */
    public ConnectorAutopilotRunResponse getAutopilotRun(String autopilotId) {
        return http.getNoFirm("/connector/autopilot/" + HttpClient.encode(autopilotId), ConnectorAutopilotRunResponse.class);
    }

    /**
     * Send a shadow-validated or staged Autopilot run.
     *
     * @param autopilotId Connector Autopilot run ID
     * @return Autopilot run lifecycle response
     */
    public ConnectorAutopilotRunResponse sendAutopilotRun(String autopilotId) {
        return http.postNoFirm(
                "/connector/autopilot/" + HttpClient.encode(autopilotId) + "/send",
                Map.of(),
                ConnectorAutopilotRunResponse.class
        );
    }

    /**
     * List Connector reconciliation items for ERP state sync.
     *
     * @param params optional reconciliation filters
     * @return reconciliation items
     */
    public ConnectorReconcileResponse reconcile(ConnectorReconcileParams params) {
        Map<String, Object> qp = new LinkedHashMap<>();
        if (params != null) {
            qp.put("status", params.status());
            qp.put("since", params.since());
        }
        return http.getNoFirm("/connector/reconcile" + HttpClient.buildQuery(qp), ConnectorReconcileResponse.class);
    }

    /**
     * List Connector reconciliation exceptions with default params.
     *
     * @return reconciliation items
     */
    public ConnectorReconcileResponse reconcile() {
        return reconcile(ConnectorReconcileParams.empty());
    }

    /**
     * List Connector-managed customer mailboxes.
     *
     * @return mailbox list response
     */
    public ConnectorMailboxListResponse mailboxes() {
        return http.getNoFirm("/connector/mailbox", ConnectorMailboxListResponse.class);
    }

    /**
     * Repair Connector mailbox state for one customer or all customers.
     *
     * @param request optional repair request body
     * @return repair result
     */
    @SuppressWarnings("unchecked")
    public Map<String, Object> repairMailbox(ConnectorMailboxRepairRequest request) {
        return (Map<String, Object>) http.postNoFirm(
                "/connector/mailbox/repair",
                request == null ? ConnectorMailboxRepairRequest.empty() : request,
                Map.class
        );
    }

    /**
     * Repair Connector mailbox state for all customers.
     *
     * @return repair result
     */
    public Map<String, Object> repairMailbox() {
        return repairMailbox(ConnectorMailboxRepairRequest.empty());
    }

    /**
     * Update the managed send policy for a Connector mailbox.
     *
     * @param customerRef Connector mailbox customer reference
     * @param request send policy request
     * @return updated mailbox response
     */
    public ConnectorMailboxUpdateResponse updateMailboxSendPolicy(String customerRef, ConnectorSendPolicyOptions request) {
        return http.patchNoFirm(
                "/connector/mailbox/" + HttpClient.encode(customerRef) + "/send-policy",
                request,
                ConnectorMailboxUpdateResponse.class
        );
    }

    /**
     * List Connector sync items for ERP reconciliation cursors.
     *
     * @param params optional sync filters
     * @return sync page
     */
    public ConnectorSyncResponse sync(ConnectorSyncParams params) {
        Map<String, Object> qp = new LinkedHashMap<>();
        if (params != null) {
            qp.put("customerRef", params.customerRef());
            qp.put("cursor", params.cursor());
            qp.put("limit", params.limit());
        }
        return http.getNoFirm("/connector/sync" + HttpClient.buildQuery(qp), ConnectorSyncResponse.class);
    }

    /**
     * List Connector sync items with default parameters.
     *
     * @return sync page
     */
    public ConnectorSyncResponse sync() {
        return sync(ConnectorSyncParams.empty());
    }

    /**
     * Retrieve a Connector document lifecycle snapshot.
     *
     * @param documentId Connector document ID
     * @return document lifecycle snapshot
     */
    public Map<String, Object> getDocument(String documentId) {
        return http.getTypedNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId),
                new TypeToken<Map<String, Object>>() {
                }
        );
    }

    Map<String, Object> getCustomerDocument(String documentId, String customerRef) {
        return http.getTypedNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId) + customerRefQuery(customerRef),
                new TypeToken<Map<String, Object>>() {
                }
        );
    }

    ConnectorBusinessDocument getBusinessDocument(String documentId, String customerRef) {
        return http.getNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId) + customerRefQuery(customerRef),
                ConnectorBusinessDocument.class
        );
    }

    ConnectorBusinessDocumentListResponse listCustomerDocuments(
            String customerRef,
            ConnectorBusinessDocumentListParams params
    ) {
        Map<String, Object> qp = new LinkedHashMap<>();
        qp.put("customerRef", customerRef);
        if (params != null) {
            qp.put("direction", params.direction());
            qp.put("state", params.state());
            qp.put("type", params.type());
            qp.put("createdAfter", params.createdAfter());
            qp.put("cursor", params.cursor());
            qp.put("limit", params.limit());
        }
        return http.getNoFirm(
                "/connector/documents" + HttpClient.buildQuery(qp),
                ConnectorBusinessDocumentListResponse.class
        );
    }

    ConnectorBusinessAcknowledgeResponse acknowledgeDocument(String documentId, String reference, String customerRef) {
        if (reference == null || reference.isBlank()) {
            throw new IllegalArgumentException("Connector reference is required");
        }
        return http.postRetryableNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId) + "/acknowledge" + customerRefQuery(customerRef),
                Map.of("reference", reference),
                ConnectorBusinessAcknowledgeResponse.class
        );
    }

    ConnectorInvoiceResponseResult respondDocument(
            String documentId,
            String customerRef,
            ConnectorInvoiceResponseRequest request
    ) {
        if (documentId == null || documentId.isBlank()) {
            throw new IllegalArgumentException("Connector documentId is required");
        }
        String normalizedCustomerRef = customerRef == null ? "" : trimString(customerRef);
        if (normalizedCustomerRef.isEmpty()) {
            throw new IllegalArgumentException("Connector customerRef is required");
        }
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("status", request.status());
        if (request.note() != null) body.put("note", request.note());
        return http.postRetryableNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId.trim()) + "/respond"
                        + customerRefQuery(normalizedCustomerRef),
                body,
                ConnectorInvoiceResponseResult.class
        );
    }

    ConnectorBusinessDocument sendDocument(String documentId, String customerRef) {
        return transitionDocument(documentId, "send", customerRef);
    }

    ConnectorBusinessDocument cancelDocument(String documentId, String customerRef) {
        return transitionDocument(documentId, "cancel", customerRef);
    }

    private ConnectorBusinessDocument transitionDocument(String documentId, String action, String customerRef) {
        if (documentId == null || documentId.isBlank()) {
            throw new IllegalArgumentException("Connector documentId is required");
        }
        return http.postRetryableNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId) + "/" + action + customerRefQuery(customerRef),
                null,
                ConnectorBusinessDocument.class
        );
    }

    /**
     * Download a Connector document UBL XML body.
     *
     * @param documentId Connector document ID
     * @return UBL XML
     */
    public String getDocumentUbl(String documentId) {
        return getDocumentUbl(documentId, null);
    }

    String getDocumentUbl(String documentId, String customerRef) {
        return http.getStringNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId) + "/ubl" + customerRefQuery(customerRef)
        );
    }

    /**
     * Retrieve Connector document delivery evidence.
     *
     * @param documentId Connector document ID
     * @return evidence payload
     */
    public Map<String, Object> getDocumentEvidence(String documentId) {
        return getDocumentEvidence(documentId, null);
    }

    Map<String, Object> getDocumentEvidence(String documentId, String customerRef) {
        return http.getTypedNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId) + "/evidence" + customerRefQuery(customerRef),
                new TypeToken<Map<String, Object>>() {
                }
        );
    }

    /**
     * Retrieve the Connector evidence bundle manifest.
     *
     * @param documentId Connector document ID
     * @return evidence bundle manifest
     */
    public Map<String, Object> getDocumentEvidenceBundle(String documentId) {
        return getDocumentEvidenceBundle(documentId, null);
    }

    Map<String, Object> getDocumentEvidenceBundle(String documentId, String customerRef) {
        return http.getTypedNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId) + "/evidence-bundle" + customerRefQuery(customerRef),
                new TypeToken<Map<String, Object>>() {
                }
        );
    }

    public Map<String, Object> getDocumentSupportPacket(String documentId) {
        return getDocumentSupportPacket(documentId, null);
    }

    Map<String, Object> getDocumentSupportPacket(String documentId, String customerRef) {
        return http.getTypedNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId) + "/support-packet" + customerRefQuery(customerRef),
                new TypeToken<Map<String, Object>>() {
                }
        );
    }

    public Map<String, Object> supportPacket(String documentId) {
        return getDocumentSupportPacket(documentId);
    }

    private static String customerRefQuery(String customerRef) {
        Map<String, Object> qp = new LinkedHashMap<>();
        qp.put("customerRef", customerRef);
        return HttpClient.buildQuery(qp);
    }

    /**
     * Execute a pending Connector action.
     *
     * @param actionId Connector action ID
     * @param request optional action request body
     * @return action result
     */
    public ConnectorActionResponse runAction(String actionId, ConnectorActionRequest request) {
        return http.postNoFirm(
                "/connector/actions/" + HttpClient.encode(actionId),
                request == null ? ConnectorActionRequest.empty() : request,
                ConnectorActionResponse.class
        );
    }

    /**
     * Execute a pending Connector action with an empty request body.
     *
     * @param actionId Connector action ID
     * @return action result
     */
    public ConnectorActionResponse runAction(String actionId) {
        return runAction(actionId, ConnectorActionRequest.empty());
    }
}
