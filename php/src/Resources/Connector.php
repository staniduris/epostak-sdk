<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Connector workflow endpoints for ERP teams.
 *
 * Legacy Connector calls use firm scoping. Connector V2 calls resolve the
 * managed firm from customerRef and omit X-Firm-Id.
 */
class Connector
{
    public ConnectorCustomers $customers;
    public ConnectorDocuments $documents;

    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
        $this->documents = new ConnectorDocuments($this);
        $this->customers = new ConnectorCustomers($this);
    }

    public function submitDocument(array $body): array
    {
        $body['mode'] ??= 'stage';
        return $this->autopilot($body);
    }

    /**
     * Validate receiver reachability and payload readiness before sending.
     *
     * @param array $body Connector preflight payload.
     * @return array Repair report and readiness response.
     * @throws EPostakError On API error.
     */
    public function preflight(array $body): array
    {
        return $this->http->request('POST', '/connector/preflight', [
            'json' => $body,
        ]);
    }

    /**
     * Send an ERP document payload through Connector.
     *
     * @param array       $body           Connector send payload.
     * @param string|null $idempotencyKey Optional Idempotency-Key header.
     * @return array Send response with documentId and status.
     * @throws EPostakError On API error.
     */
    public function send(array $body, ?string $idempotencyKey = null): array
    {
        $options = ['json' => $body];
        if ($idempotencyKey !== null) {
            $options['headers'] = ['Idempotency-Key' => $idempotencyKey];
        }
        return $this->http->request('POST', '/connector/send', $options);
    }

    /**
     * Get Connector status for a document ID.
     *
     * @param string $documentId Document UUID.
     * @return array Connector status response.
     * @throws EPostakError On API error.
     */
    public function status(string $documentId): array
    {
        return $this->http->request('GET', '/connector/status/' . urlencode($documentId));
    }

    /**
     * List Connector inbox documents with cursor pagination.
     *
     * @param array{cursor?: string, limit?: int} $params Optional pagination params.
     * @return array Connector inbox page.
     * @throws EPostakError On API error.
     */
    public function inbox(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'cursor' => $params['cursor'] ?? null,
            'limit' => $params['limit'] ?? null,
        ]);
        return $this->http->request('GET', '/connector/inbox' . $qs);
    }

    /**
     * Retrieve a single Connector inbox document.
     *
     * @param string $documentId Document UUID.
     * @return array Connector inbox document.
     * @throws EPostakError On API error.
     */
    public function getInboxDocument(string $documentId): array
    {
        return $this->http->request('GET', '/connector/inbox/' . urlencode($documentId));
    }

    /**
     * Acknowledge a Connector inbox document as processed.
     *
     * @param string $documentId Document UUID.
     * @return array Connector ack response.
     * @throws EPostakError On API error.
     */
    public function ack(string $documentId): array
    {
        return $this->http->request('POST', '/connector/inbox/' . urlencode($documentId) . '/ack', [
            'json' => [],
        ]);
    }

    /**
     * List Connector polling events with cursor pagination.
     *
     * @param array{cursor?: string, limit?: int} $params Optional pagination params.
     * @return array Connector events page.
     * @throws EPostakError On API error.
     */
    public function events(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'cursor' => $params['cursor'] ?? null,
            'limit' => $params['limit'] ?? null,
        ]);
        return $this->http->request('GET', '/connector/events' . $qs);
    }

    /**
     * Stage one or more ERP invoices without immediate Peppol delivery.
     *
     * @param array $body Connector outbox staging payload.
     * @return array Stage response with items and repair reports.
     * @throws EPostakError On API error.
     */
    public function stageOutbox(array $body): array
    {
        return $this->http->request('POST', '/connector/outbox', [
            'json' => $body,
        ]);
    }

    /**
     * List staged Connector outbox items.
     *
     * @param array{status?: string, limit?: int, offset?: int} $params Optional list params.
     * @return array Connector outbox list response.
     * @throws EPostakError On API error.
     */
    public function listOutbox(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'status' => $params['status'] ?? null,
            'limit' => $params['limit'] ?? null,
            'offset' => $params['offset'] ?? null,
        ]);
        return $this->http->request('GET', '/connector/outbox' . $qs);
    }

    /**
     * Retrieve a single Connector outbox item.
     *
     * @param string $outboxId Connector outbox item ID.
     * @return array Connector outbox item.
     * @throws EPostakError On API error.
     */
    public function getOutboxItem(string $outboxId): array
    {
        return $this->http->request('GET', '/connector/outbox/' . urlencode($outboxId));
    }

    /**
     * Send one staged outbox item through the Connector workflow.
     *
     * @param string    $outboxId Connector outbox item ID.
     * @param bool|null $force    Send before scheduledFor when true.
     * @return array Connector outbox item or blocked repair report.
     * @throws EPostakError On API error.
     */
    public function sendOutboxItem(string $outboxId, ?bool $force = null): array
    {
        $body = [];
        if ($force !== null) {
            $body['force'] = $force;
        }
        return $this->http->request('POST', '/connector/outbox/' . urlencode($outboxId) . '/send', [
            'json' => $body,
        ]);
    }

    /**
     * Send ready, failed, or due scheduled outbox items in a batch.
     *
     * @param array{ids?: string[], limit?: int, force?: bool} $body Batch send body.
     * @return array Per-item batch send response.
     * @throws EPostakError On API error.
     */
    public function sendOutboxBatch(array $body = []): array
    {
        return $this->http->request('POST', '/connector/outbox/send', [
            'json' => $body,
        ]);
    }

    /**
     * Cancel a staged outbox item before it is sent.
     *
     * @param string $outboxId Connector outbox item ID.
     * @return array Cancelled Connector outbox item.
     * @throws EPostakError On API error.
     */
    public function cancelOutboxItem(string $outboxId): array
    {
        return $this->http->request('DELETE', '/connector/outbox/' . urlencode($outboxId));
    }

    /**
     * Start a managed Connector Autopilot lifecycle run.
     *
     * @param array $body Autopilot request with mode, payload, and optional IDs.
     * @return array Autopilot run lifecycle response.
     * @throws EPostakError On API error.
     */
    public function autopilot(array $body): array
    {
        return $this->http->request('POST', '/connector/autopilot', [
            'json' => $body,
            'omitFirmId' => true,
        ]);
    }

    /**
     * Map a saved Connector Mapper template input into preview, stage, or send.
     *
     * @param array $body Mapper request with templateKey and source payload.
     * @return array Mapping preview, checklist, or Autopilot result.
     * @throws EPostakError On API error.
     */
    public function mapper(array $body): array
    {
        return $this->http->request('POST', '/connector/mapper', [
            'json' => $body,
            'omitFirmId' => true,
        ]);
    }

    /**
     * Normalize a loose ERP/customer payload into a Connector lifecycle run.
     *
     * @param array $body Zen input request with customerRef and invoice/customer fields.
     * @return array Autopilot run lifecycle response.
     * @throws EPostakError On API error.
     */
    public function zenInput(array $body): array
    {
        return $this->http->request('POST', '/connector/zen-input', [
            'json' => $body,
            'omitFirmId' => true,
        ]);
    }

    /**
     * Retrieve an Autopilot run by ID.
     *
     * @param string $autopilotId Connector Autopilot run ID.
     * @return array Autopilot run lifecycle response.
     * @throws EPostakError On API error.
     */
    public function getAutopilotRun(string $autopilotId): array
    {
        return $this->http->request('GET', '/connector/autopilot/' . urlencode($autopilotId), [
            'omitFirmId' => true,
        ]);
    }

    /**
     * Send a shadow-validated or staged Autopilot run.
     *
     * @param string $autopilotId Connector Autopilot run ID.
     * @return array Autopilot run lifecycle response.
     * @throws EPostakError On API error.
     */
    public function sendAutopilotRun(string $autopilotId): array
    {
        return $this->http->request('POST', '/connector/autopilot/' . urlencode($autopilotId) . '/send', [
            'json' => (object) [],
            'omitFirmId' => true,
        ]);
    }

    /**
     * List Connector reconciliation items for ERP state sync.
     *
     * @param array{status?: string, since?: string} $params Optional reconciliation params.
     * @return array Reconciliation items.
     * @throws EPostakError On API error.
     */
    public function reconcile(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'status' => $params['status'] ?? null,
            'since' => $params['since'] ?? null,
        ]);
        return $this->http->request('GET', '/connector/reconcile' . $qs, [
            'omitFirmId' => true,
        ]);
    }

    /**
     * List Connector-managed customer mailboxes.
     *
     * @return array Mailbox list response.
     * @throws EPostakError On API error.
     */
    public function mailboxes(): array
    {
        return $this->http->request('GET', '/connector/mailbox', [
            'omitFirmId' => true,
        ]);
    }

    /**
     * Repair Connector mailbox state for one customer or all customers.
     *
     * @param array{customerRef?: string} $body Optional repair request body.
     * @return array Repair result.
     * @throws EPostakError On API error.
     */
    public function repairMailbox(array $body = []): array
    {
        return $this->http->request('POST', '/connector/mailbox/repair', [
            'json' => $body === [] ? (object) [] : $body,
            'omitFirmId' => true,
        ]);
    }

    /**
     * Update the managed send policy for a Connector mailbox.
     *
     * @param string $customerRef Connector mailbox customer reference.
     * @param array{policy: string, sendAt?: string} $body Send policy request.
     * @return array Updated mailbox response.
     * @throws EPostakError On API error.
     */
    public function updateMailboxSendPolicy(string $customerRef, array $body): array
    {
        return $this->http->request('PATCH', '/connector/mailbox/' . urlencode($customerRef) . '/send-policy', [
            'json' => $body,
            'omitFirmId' => true,
        ]);
    }

    /**
     * List Connector sync items for ERP reconciliation cursors.
     *
     * @param array{customerRef?: string, cursor?: string, limit?: int} $params Optional sync params.
     * @return array Sync page.
     * @throws EPostakError On API error.
     */
    public function sync(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'customerRef' => $params['customerRef'] ?? null,
            'cursor' => $params['cursor'] ?? null,
            'limit' => $params['limit'] ?? null,
        ]);
        return $this->http->request('GET', '/connector/sync' . $qs, [
            'omitFirmId' => true,
        ]);
    }

    /**
     * Retrieve a Connector document lifecycle snapshot.
     *
     * @param string $documentId Connector document ID.
     * @return array Document lifecycle snapshot.
     * @throws EPostakError On API error.
     */
    public function getDocument(string $documentId): array
    {
        return $this->http->request('GET', '/connector/documents/' . urlencode($documentId), [
            'omitFirmId' => true,
        ]);
    }

    /**
     * Download a Connector document UBL XML body.
     *
     * @param string $documentId Connector document ID.
     * @return string UBL XML.
     * @throws EPostakError On API error.
     */
    public function getDocumentUbl(string $documentId): string
    {
        return $this->http->requestRaw('GET', '/connector/documents/' . urlencode($documentId) . '/ubl', true);
    }

    /**
     * Retrieve Connector document delivery evidence.
     *
     * @param string $documentId Connector document ID.
     * @return array Evidence payload.
     * @throws EPostakError On API error.
     */
    public function getDocumentEvidence(string $documentId): array
    {
        return $this->http->request('GET', '/connector/documents/' . urlencode($documentId) . '/evidence', [
            'omitFirmId' => true,
        ]);
    }

    /**
     * Retrieve the Connector evidence bundle manifest.
     *
     * @param string $documentId Connector document ID.
     * @return array Evidence bundle manifest.
     * @throws EPostakError On API error.
     */
    public function getDocumentEvidenceBundle(string $documentId): array
    {
        return $this->http->request('GET', '/connector/documents/' . urlencode($documentId) . '/evidence-bundle', [
            'omitFirmId' => true,
        ]);
    }

    public function getDocumentSupportPacket(string $documentId): array
    {
        return $this->http->request('GET', '/connector/documents/' . urlencode($documentId) . '/support-packet', [
            'omitFirmId' => true,
        ]);
    }

    /**
     * Execute a pending Connector action.
     *
     * @param string $actionId Connector action ID.
     * @param array{sendAt?: string, status?: string, note?: string} $body Optional action request body.
     * @return array Action result.
     * @throws EPostakError On API error.
     */
    public function runAction(string $actionId, array $body = []): array
    {
        return $this->http->request('POST', '/connector/actions/' . urlencode($actionId), [
            'json' => $body === [] ? (object) [] : $body,
            'omitFirmId' => true,
        ]);
    }
}

function connector_with_customer_ref(string $customerRef, array $body): array
{
    if (isset($body['customerRef']) && $body['customerRef'] !== $customerRef) {
        throw new \InvalidArgumentException('Connector customerRef conflicts with scoped customer');
    }
    $body['customerRef'] = $customerRef;
    return $body;
}

class ConnectorCustomers
{
    public function __construct(private Connector $connector)
    {
    }

    public function for(string $customerRef): ConnectorCustomer
    {
        return new ConnectorCustomer($this->connector, $customerRef);
    }
}

class ConnectorCustomer
{
    public ConnectorCustomerDocuments $documents;
    public ConnectorCustomerMailbox $mailbox;

    public function __construct(
        private Connector $connector,
        private string $customerRef
    ) {
        $this->documents = new ConnectorCustomerDocuments($connector);
        $this->mailbox = new ConnectorCustomerMailbox($connector, $customerRef);
    }

    public function submitDocument(array $body): array
    {
        $body = connector_with_customer_ref($this->customerRef, $body);
        $body['mode'] ??= 'stage';
        return $this->connector->autopilot($body);
    }

    public function autopilot(array $body): array
    {
        return $this->connector->autopilot(connector_with_customer_ref($this->customerRef, $body));
    }

    public function mapper(array $body): array
    {
        return $this->connector->mapper(connector_with_customer_ref($this->customerRef, $body));
    }

    public function zenInput(array $body): array
    {
        return $this->connector->zenInput(connector_with_customer_ref($this->customerRef, $body));
    }

    public function sync(array $params = []): array
    {
        return $this->connector->sync(connector_with_customer_ref($this->customerRef, $params));
    }
}

class ConnectorDocuments
{
    public function __construct(private Connector $connector)
    {
    }

    public function get(string $documentId): array
    {
        return $this->connector->getDocument($documentId);
    }

    public function ubl(string $documentId): string
    {
        return $this->connector->getDocumentUbl($documentId);
    }

    public function evidence(string $documentId): array
    {
        return $this->connector->getDocumentEvidence($documentId);
    }

    public function evidenceBundle(string $documentId): array
    {
        return $this->connector->getDocumentEvidenceBundle($documentId);
    }

    public function supportPacket(string $documentId): array
    {
        return $this->connector->getDocumentSupportPacket($documentId);
    }
}

class ConnectorCustomerDocuments extends ConnectorDocuments
{
}

class ConnectorCustomerMailbox
{
    public function __construct(
        private Connector $connector,
        private string $customerRef
    ) {
    }

    public function repair(): array
    {
        return $this->connector->repairMailbox(['customerRef' => $this->customerRef]);
    }

    public function updateSendPolicy(array $body): array
    {
        return $this->connector->updateMailboxSendPolicy($this->customerRef, $body);
    }
}
