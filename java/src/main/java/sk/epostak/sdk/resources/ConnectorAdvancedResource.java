package sk.epostak.sdk.resources;

import sk.epostak.sdk.models.*;

import java.util.Map;

/**
 * Explicit opt-in surface for lower-level and compatibility Connector APIs.
 * <p>
 * Most integrations only need
 * {@code connector().customers().forCustomer(customerRef).documents()} and
 * {@code events()}. Use this resource for legacy firm-scoped delivery or for
 * advanced orchestration such as outbox, Autopilot, mapper, reconcile,
 * mailbox, sync, and action execution.
 */
@SuppressWarnings("deprecation")
public final class ConnectorAdvancedResource {
    private final ConnectorResource connector;
    private final ConnectorAdvancedDocumentsResource documents;

    ConnectorAdvancedResource(ConnectorResource connector) {
        this.connector = connector;
        this.documents = new ConnectorAdvancedDocumentsResource(connector);
    }

    public ConnectorAdvancedDocumentsResource documents() { return documents; }

    public ConnectorPreflightResponse preflight(ConnectorPreflightRequest request) {
        return connector.preflight(request);
    }

    public ConnectorSendResponse send(Map<String, Object> request) {
        return connector.send(request);
    }

    public ConnectorSendResponse send(Map<String, Object> request, String idempotencyKey) {
        return connector.send(request, idempotencyKey);
    }

    public ConnectorStatusResponse status(String documentId) {
        return connector.status(documentId);
    }

    public ConnectorInboxListResponse inbox(ConnectorListParams params) {
        return connector.inbox(params);
    }

    public ConnectorInboxListResponse inbox() {
        return connector.inbox();
    }

    public ConnectorInboxDocument getInboxDocument(String documentId) {
        return connector.getInboxDocument(documentId);
    }

    public ConnectorAckResponse ack(String documentId) {
        return connector.ack(documentId);
    }

    /** Firm-scoped technical event feed with legacy {@code status} events. */
    public ConnectorEventsResponse events(ConnectorListParams params) {
        return connector.events(params);
    }

    public ConnectorEventsResponse events() {
        return connector.events();
    }

    public ConnectorOutboxStageResponse stageOutbox(ConnectorOutboxStageRequest request) {
        return connector.stageOutbox(request);
    }

    public ConnectorOutboxListResponse listOutbox(ConnectorOutboxListParams params) {
        return connector.listOutbox(params);
    }

    public ConnectorOutboxListResponse listOutbox() {
        return connector.listOutbox();
    }

    public ConnectorOutboxItem getOutboxItem(String outboxId) {
        return connector.getOutboxItem(outboxId);
    }

    public ConnectorOutboxItem sendOutboxItem(String outboxId, ConnectorOutboxSendOptions options) {
        return connector.sendOutboxItem(outboxId, options);
    }

    public ConnectorOutboxItem sendOutboxItem(String outboxId) {
        return connector.sendOutboxItem(outboxId);
    }

    public ConnectorOutboxBatchSendResponse sendOutboxBatch(ConnectorOutboxBatchSendRequest request) {
        return connector.sendOutboxBatch(request);
    }

    public ConnectorOutboxBatchSendResponse sendOutboxBatch() {
        return connector.sendOutboxBatch();
    }

    public ConnectorOutboxItem cancelOutboxItem(String outboxId) {
        return connector.cancelOutboxItem(outboxId);
    }

    public ConnectorAutopilotRunResponse autopilot(ConnectorAutopilotRequest request) {
        return connector.autopilot(request);
    }

    public Map<String, Object> mapper(Map<String, Object> request) {
        return connector.mapper(request);
    }

    public ConnectorAutopilotRunResponse zenInput(Map<String, Object> request) {
        return connector.zenInput(request);
    }

    public ConnectorAutopilotRunResponse getAutopilotRun(String autopilotId) {
        return connector.getAutopilotRun(autopilotId);
    }

    public ConnectorAutopilotRunResponse sendAutopilotRun(String autopilotId) {
        return connector.sendAutopilotRun(autopilotId);
    }

    public ConnectorReconcileResponse reconcile(ConnectorReconcileParams params) {
        return connector.reconcile(params);
    }

    public ConnectorReconcileResponse reconcile() {
        return connector.reconcile();
    }

    public ConnectorMailboxListResponse mailboxes() {
        return connector.mailboxes();
    }

    public Map<String, Object> repairMailbox(ConnectorMailboxRepairRequest request) {
        return connector.repairMailbox(request);
    }

    public Map<String, Object> repairMailbox() {
        return connector.repairMailbox();
    }

    public ConnectorMailboxUpdateResponse updateMailboxSendPolicy(
            String customerRef,
            ConnectorSendPolicyOptions request
    ) {
        return connector.updateMailboxSendPolicy(customerRef, request);
    }

    public ConnectorSyncResponse sync(ConnectorSyncParams params) {
        return connector.sync(params);
    }

    public ConnectorSyncResponse sync() {
        return connector.sync();
    }

    public ConnectorActionResponse runAction(String actionId, ConnectorActionRequest request) {
        return connector.runAction(actionId, request);
    }

    public ConnectorActionResponse runAction(String actionId) {
        return connector.runAction(actionId);
    }
}
