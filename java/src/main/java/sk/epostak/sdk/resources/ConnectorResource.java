package sk.epostak.sdk.resources;

import com.google.gson.reflect.TypeToken;
import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.*;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Connector workflow endpoints for ERP teams.
 * <p>
 * Connector is a polling-first workflow over the Enterprise API. It uses the
 * same credentials, firm scoping, and documentId as the full Enterprise API.
 */
public final class ConnectorResource {

    private final HttpClient http;
    private final ConnectorCustomersResource customers;

    /**
     * Creates a new Connector resource.
     *
     * @param http the HTTP client used for API communication
     */
    public ConnectorResource(HttpClient http) {
        this.http = http;
        this.customers = new ConnectorCustomersResource(this);
    }

    public ConnectorCustomersResource customers() {
        return customers;
    }

    public ConnectorAutopilotRunResponse submitDocument(ConnectorSubmitDocumentRequest request) {
        if (request.mode() == null) {
            request.mode("stage");
        }
        return http.postNoFirm("/connector/autopilot", request.toMap(), ConnectorAutopilotRunResponse.class);
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

    /**
     * List Connector polling events with default parameters.
     *
     * @return first Connector events page
     */
    public ConnectorEventsResponse events() {
        return events(ConnectorListParams.empty());
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

    /**
     * Download a Connector document UBL XML body.
     *
     * @param documentId Connector document ID
     * @return UBL XML
     */
    public String getDocumentUbl(String documentId) {
        return http.getStringNoFirm("/connector/documents/" + HttpClient.encode(documentId) + "/ubl");
    }

    /**
     * Retrieve Connector document delivery evidence.
     *
     * @param documentId Connector document ID
     * @return evidence payload
     */
    public Map<String, Object> getDocumentEvidence(String documentId) {
        return http.getTypedNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId) + "/evidence",
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
        return http.getTypedNoFirm(
                "/connector/documents/" + HttpClient.encode(documentId) + "/evidence-bundle",
                new TypeToken<Map<String, Object>>() {
                }
        );
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
