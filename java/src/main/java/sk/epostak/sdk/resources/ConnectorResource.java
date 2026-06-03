package sk.epostak.sdk.resources;

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

    /**
     * Creates a new Connector resource.
     *
     * @param http the HTTP client used for API communication
     */
    public ConnectorResource(HttpClient http) {
        this.http = http;
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
}
