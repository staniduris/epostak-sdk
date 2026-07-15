<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/** Match the backend's ECMAScript TrimString code-point contract. */
function connector_trim_string(string $value): string
{
    return (string) preg_replace(
        '/\A[\x{0009}-\x{000D}\x{0020}\x{00A0}\x{1680}\x{2000}-\x{200A}\x{2028}\x{2029}\x{202F}\x{205F}\x{3000}\x{FEFF}]++|[\x{0009}-\x{000D}\x{0020}\x{00A0}\x{1680}\x{2000}-\x{200A}\x{2028}\x{2029}\x{202F}\x{205F}\x{3000}\x{FEFF}]++\z/u',
        '',
        $value
    );
}

function connector_idempotency_key(string $value): string
{
    if (connector_trim_string($value) === '' || strlen($value) > 255) {
        throw new \InvalidArgumentException('Connector idempotency key must be 1-255 UTF-8 bytes');
    }
    return $value;
}

/**
 * Connector workflow endpoints for ERP teams.
 *
 * The primary API is customers->for($ref)->documents and ->events. Direct
 * preflight/send/outbox/inbox/Autopilot/sync methods are supported
 * compatibility aliases for the same methods under ->advanced.
 */
class Connector
{
    private const INVOICE_RESPONSE_STATUSES = [
        'received',
        'in_process',
        'under_query',
        'conditionally_accepted',
        'rejected',
        'accepted',
        'paid',
    ];

    public ConnectorCustomers $customers;
    public ConnectorDocuments $documents;
    public ConnectorWebhook $webhook;
    public ConnectorAdvanced $advanced;

    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
        $this->documents = new ConnectorDocuments($this);
        $this->customers = new ConnectorCustomers($this);
        $this->webhook = new ConnectorWebhook($http);
        $this->advanced = new ConnectorAdvanced($this);
    }

    /**
     *             Use customer->documents->send()/stage() for business JSON.
     */
    public function submitDocument(array $body): array
    {
        $body['mode'] ??= 'stage';
        return $this->autopilot($body);
    }

    /** Stable bounded key for a length-prefixed UTF-8 tuple. */
    private static function documentIdempotencyKey(string $customerRef, string $externalId): string
    {
        $customerRef = connector_trim_string($customerRef);
        $externalId = connector_trim_string($externalId);
        $tuple = pack('N', strlen($customerRef)) . $customerRef
            . pack('N', strlen($externalId)) . $externalId;
        return 'connector:v1:' . hash('sha256', $tuple);
    }

    public function submitCustomerDocument(
        string $customerRef,
        array $body,
        string $delivery = 'send',
        ?string $idempotencyKey = null
    ): array
    {
        if (connector_trim_string($customerRef) === '') {
            throw new \InvalidArgumentException('Connector customerRef is required');
        }
        $externalId = connector_trim_string((string) ($body['externalId'] ?? ''));
        if ($externalId === '') {
            throw new \InvalidArgumentException('Connector externalId is required');
        }
        if (trim((string) ($body['number'] ?? '')) === '') {
            throw new \InvalidArgumentException('Connector number is required');
        }
        $recipient = $body['recipient'] ?? null;
        if (!is_array($recipient) || trim((string) ($recipient['country'] ?? '')) === '') {
            throw new \InvalidArgumentException('Connector recipient.country is required');
        }
        $recipientIds = array_filter([
            $recipient['companyId'] ?? null,
            $recipient['taxId'] ?? null,
            $recipient['vatId'] ?? null,
            $recipient['networkId'] ?? null,
        ], static fn ($value): bool => trim((string) $value) !== '');
        if ($recipientIds === []) {
            throw new \InvalidArgumentException('Connector recipient requires companyId, taxId, vatId, or networkId');
        }
        if (!isset($body['lines']) || !is_array($body['lines']) || $body['lines'] === []) {
            throw new \InvalidArgumentException('Connector lines must contain at least one item');
        }
        $customerRef = connector_trim_string($customerRef);
        $body['customerRef'] = $customerRef;
        $body['externalId'] = $externalId;
        $body['delivery'] = $delivery;
        $key = $idempotencyKey === null
            ? self::documentIdempotencyKey($customerRef, $externalId)
            : connector_idempotency_key($idempotencyKey);
        return $this->http->request('POST', '/connector/documents', [
            'json' => $body,
            'headers' => ['Idempotency-Key' => $key],
            'omitFirmId' => true,
            'retryOnFailure' => true,
        ]);
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

    /** Customer-scoped business events expose state, unlike legacy status events. */
    public function customerEvents(string $customerRef, array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'customerRef' => connector_trim_string($customerRef),
            'cursor' => $params['cursor'] ?? null,
            'limit' => $params['limit'] ?? null,
        ]);
        return $this->http->request('GET', '/connector/events' . $qs, ['omitFirmId' => true]);
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
    public function getDocument(string $documentId, ?string $customerRef = null): array
    {
        $qs = HttpClient::buildQuery(['customerRef' => $customerRef]);
        return $this->http->request('GET', '/connector/documents/' . urlencode($documentId) . $qs, [
            'omitFirmId' => true,
        ]);
    }

    public function listCustomerDocuments(string $customerRef, array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'customerRef' => $customerRef,
            'direction' => $params['direction'] ?? null,
            'state' => $params['state'] ?? null,
            'type' => $params['type'] ?? null,
            'createdAfter' => $params['createdAfter'] ?? null,
            'cursor' => $params['cursor'] ?? null,
            'limit' => $params['limit'] ?? null,
        ]);
        return $this->http->request('GET', '/connector/documents' . $qs, [
            'omitFirmId' => true,
        ]);
    }

    public function acknowledgeDocument(string $documentId, string $reference, ?string $customerRef = null): array
    {
        if (trim($reference) === '') {
            throw new \InvalidArgumentException('Connector reference is required');
        }
        $qs = HttpClient::buildQuery(['customerRef' => $customerRef]);
        return $this->http->request('POST', '/connector/documents/' . urlencode($documentId) . '/acknowledge' . $qs, [
            'json' => ['reference' => $reference],
            'omitFirmId' => true,
            'retryOnFailure' => true,
        ]);
    }

    public function respondDocument(string $documentId, string $customerRef, array $body): array
    {
        $documentId = trim($documentId);
        $customerRef = connector_trim_string($customerRef);
        $status = $body['status'] ?? null;
        if ($documentId === '') {
            throw new \InvalidArgumentException('Connector documentId is required');
        }
        if ($customerRef === '') {
            throw new \InvalidArgumentException('Connector customerRef is required');
        }
        if (!is_string($status) || !in_array($status, self::INVOICE_RESPONSE_STATUSES, true)) {
            throw new \InvalidArgumentException('Invalid Connector response status');
        }
        if (array_key_exists('note', $body) && !is_string($body['note'])) {
            throw new \InvalidArgumentException('Connector response note must be a string');
        }
        $payload = ['status' => $status];
        if (array_key_exists('note', $body)) {
            $payload['note'] = $body['note'];
        }
        $qs = HttpClient::buildQuery(['customerRef' => $customerRef]);
        return $this->http->request('POST', '/connector/documents/' . urlencode($documentId) . '/respond' . $qs, [
            'json' => $payload,
            'omitFirmId' => true,
            'retryOnFailure' => true,
        ]);
    }

    /** Send a previously staged customer document. */
    public function sendCustomerDocument(string $documentId, ?string $customerRef = null): array
    {
        if (trim($documentId) === '') {
            throw new \InvalidArgumentException('Connector documentId is required');
        }
        $qs = HttpClient::buildQuery(['customerRef' => $customerRef]);
        return $this->http->request('POST', '/connector/documents/' . urlencode($documentId) . '/send' . $qs, [
            'omitFirmId' => true,
            'retryOnFailure' => true,
        ]);
    }

    /** Cancel a staged customer document before delivery starts. */
    public function cancelCustomerDocument(string $documentId, ?string $customerRef = null): array
    {
        if (trim($documentId) === '') {
            throw new \InvalidArgumentException('Connector documentId is required');
        }
        $qs = HttpClient::buildQuery(['customerRef' => $customerRef]);
        return $this->http->request('POST', '/connector/documents/' . urlencode($documentId) . '/cancel' . $qs, [
            'omitFirmId' => true,
            'retryOnFailure' => true,
        ]);
    }

    /**
     * Download a Connector document UBL XML body.
     *
     * @param string $documentId Connector document ID.
     * @return string UBL XML.
     * @throws EPostakError On API error.
     */
    public function getDocumentUbl(string $documentId, ?string $customerRef = null): string
    {
        $qs = HttpClient::buildQuery(['customerRef' => $customerRef]);
        return $this->http->requestRaw('GET', '/connector/documents/' . urlencode($documentId) . '/ubl' . $qs, true);
    }

    /**
     * Retrieve Connector document delivery evidence.
     *
     * @param string $documentId Connector document ID.
     * @return array Evidence payload.
     * @throws EPostakError On API error.
     */
    public function getDocumentEvidence(string $documentId, ?string $customerRef = null): array
    {
        $qs = HttpClient::buildQuery(['customerRef' => $customerRef]);
        return $this->http->request('GET', '/connector/documents/' . urlencode($documentId) . '/evidence' . $qs, [
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
    public function getDocumentEvidenceBundle(string $documentId, ?string $customerRef = null): array
    {
        $qs = HttpClient::buildQuery(['customerRef' => $customerRef]);
        return $this->http->request('GET', '/connector/documents/' . urlencode($documentId) . '/evidence-bundle' . $qs, [
            'omitFirmId' => true,
        ]);
    }

    public function getDocumentSupportPacket(string $documentId, ?string $customerRef = null): array
    {
        $qs = HttpClient::buildQuery(['customerRef' => $customerRef]);
        return $this->http->request('GET', '/connector/documents/' . urlencode($documentId) . '/support-packet' . $qs, [
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

/** One global Connector webhook configuration per integrator. */
class ConnectorWebhook
{
    public function __construct(private HttpClient $http)
    {
    }

    /** Get the integrator's Connector webhook configuration. */
    public function get(): array
    {
        return $this->http->request('GET', '/connector/webhook', [
            'omitFirmId' => true,
        ]);
    }

    /** Create or replace the integrator's Connector webhook. */
    public function configure(string $url, ?array $events = null): array
    {
        $url = trim($url);
        if ($url === '') {
            throw new \InvalidArgumentException('Connector webhook URL is required');
        }
        $body = ['url' => $url];
        if ($events !== null) {
            $body['events'] = array_values($events);
        }
        return $this->http->request('PUT', '/connector/webhook', [
            'json' => $body,
            'omitFirmId' => true,
        ]);
    }

    /** Delete the integrator's Connector webhook configuration. */
    public function delete(): void
    {
        $this->http->request('DELETE', '/connector/webhook', [
            'omitFirmId' => true,
        ]);
    }

    /** Rotate and return the Connector webhook signing secret. */
    public function rotateSecret(): array
    {
        return $this->http->request('POST', '/connector/webhook/rotate-secret', [
            'omitFirmId' => true,
        ]);
    }

    /** Send a canonical test event for one approved customer. */
    public function test(string $customerRef): array
    {
        $customerRef = connector_trim_string($customerRef);
        if ($customerRef === '') {
            throw new \InvalidArgumentException('Connector customerRef is required');
        }
        return $this->http->request('POST', '/connector/webhook/test', [
            'json' => ['customerRef' => $customerRef],
            'omitFirmId' => true,
        ]);
    }

    /** List Connector webhook delivery attempts. */
    public function deliveries(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'cursor' => $params['cursor'] ?? null,
            'limit' => $params['limit'] ?? null,
            'status' => isset($params['status']) ? strtoupper((string) $params['status']) : null,
        ]);
        return $this->http->request('GET', '/connector/webhook/deliveries' . $qs, [
            'omitFirmId' => true,
        ]);
    }
}

/** Protocol-oriented staging queue kept outside the primary document API. */
class ConnectorAdvancedOutbox
{
    public function __construct(private Connector $connector)
    {
    }

    public function stage(array $body): array
    {
        return $this->connector->stageOutbox($body);
    }

    public function list(array $params = []): array
    {
        return $this->connector->listOutbox($params);
    }

    public function get(string $outboxId): array
    {
        return $this->connector->getOutboxItem($outboxId);
    }

    public function send(string $outboxId, ?bool $force = null): array
    {
        return $this->connector->sendOutboxItem($outboxId, $force);
    }

    public function sendBatch(array $body = []): array
    {
        return $this->connector->sendOutboxBatch($body);
    }

    public function cancel(string $outboxId): array
    {
        return $this->connector->cancelOutboxItem($outboxId);
    }
}

/**
 * Advanced and legacy Connector workflows.
 *
 * New integrations should start with
 * connector->customers->for($ref)->documents and ->events.
 */
class ConnectorAdvanced
{
    public ConnectorAdvancedOutbox $outbox;
    public ConnectorDocuments $documents;

    public function __construct(private Connector $connector)
    {
        $this->outbox = new ConnectorAdvancedOutbox($connector);
        $this->documents = $connector->documents;
    }

    public function preflight(array $body): array
    {
        return $this->connector->preflight($body);
    }

    public function send(array $body, ?string $idempotencyKey = null): array
    {
        return $this->connector->send($body, $idempotencyKey);
    }

    public function status(string $documentId): array
    {
        return $this->connector->status($documentId);
    }

    public function inbox(array $params = []): array
    {
        return $this->connector->inbox($params);
    }

    public function getInboxDocument(string $documentId): array
    {
        return $this->connector->getInboxDocument($documentId);
    }

    public function ack(string $documentId): array
    {
        return $this->connector->ack($documentId);
    }

    public function events(array $params = []): array
    {
        return $this->connector->events($params);
    }

    public function autopilot(array $body): array
    {
        return $this->connector->autopilot($body);
    }

    public function mapper(array $body): array
    {
        return $this->connector->mapper($body);
    }

    public function zenInput(array $body): array
    {
        return $this->connector->zenInput($body);
    }

    public function getAutopilotRun(string $autopilotId): array
    {
        return $this->connector->getAutopilotRun($autopilotId);
    }

    public function sendAutopilotRun(string $autopilotId): array
    {
        return $this->connector->sendAutopilotRun($autopilotId);
    }

    public function reconcile(array $params = []): array
    {
        return $this->connector->reconcile($params);
    }

    public function mailboxes(): array
    {
        return $this->connector->mailboxes();
    }

    public function repairMailbox(array $body = []): array
    {
        return $this->connector->repairMailbox($body);
    }

    public function updateMailboxSendPolicy(string $customerRef, array $body): array
    {
        return $this->connector->updateMailboxSendPolicy($customerRef, $body);
    }

    public function sync(array $params = []): array
    {
        return $this->connector->sync($params);
    }

    public function runAction(string $actionId, array $body = []): array
    {
        return $this->connector->runAction($actionId, $body);
    }
}

function connector_with_customer_ref(string $customerRef, array $body): array
{
    $customerRef = connector_trim_string($customerRef);
    if (isset($body['customerRef']) && connector_trim_string((string) $body['customerRef']) !== $customerRef) {
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
        if (connector_trim_string($customerRef) === '') {
            throw new \InvalidArgumentException('Connector customerRef is required');
        }
        return new ConnectorCustomer($this->connector, connector_trim_string($customerRef));
    }
}

class ConnectorCustomer
{
    public ConnectorCustomerDocuments $documents;
    public ConnectorCustomerEvents $events;
    public ConnectorCustomerAdvanced $advanced;
    public ConnectorCustomerMailbox $mailbox;

    public function __construct(
        private Connector $connector,
        private string $customerRef
    ) {
        $this->documents = new ConnectorCustomerDocuments($connector, $customerRef);
        $this->events = new ConnectorCustomerEvents($connector, $customerRef);
        $this->advanced = new ConnectorCustomerAdvanced($connector, $customerRef);
        $this->mailbox = $this->advanced->mailbox;
    }

    public function submitDocument(array $body): array
    {
        $body = connector_with_customer_ref($this->customerRef, $body);
        $body['mode'] ??= 'stage';
        return $this->connector->autopilot($body);
    }

    public function autopilot(array $body): array
    {
        return $this->advanced->autopilot($body);
    }

    public function mapper(array $body): array
    {
        return $this->advanced->mapper($body);
    }

    public function zenInput(array $body): array
    {
        return $this->advanced->zenInput($body);
    }

    public function sync(array $params = []): array
    {
        return $this->advanced->sync($params);
    }
}

/** Advanced helpers for one manually approved Connector customer. */
class ConnectorCustomerAdvanced
{
    public ConnectorDocuments $documents;
    public ConnectorCustomerMailbox $mailbox;

    public function __construct(
        private Connector $connector,
        private string $customerRef
    ) {
        $this->documents = new ConnectorDocuments($connector, $customerRef);
        $this->mailbox = new ConnectorCustomerMailbox($connector, $customerRef);
    }

    public function autopilot(array $body): array
    {
        return $this->connector->advanced->autopilot(connector_with_customer_ref($this->customerRef, $body));
    }

    public function mapper(array $body): array
    {
        if (isset($body['execute']) && $body['execute'] !== 'preview') {
            throw new \InvalidArgumentException('Connector Mapper only supports preview normalization');
        }
        $body['execute'] = 'preview';
        return $this->connector->advanced->mapper(connector_with_customer_ref($this->customerRef, $body));
    }

    public function zenInput(array $body): array
    {
        return $this->connector->advanced->zenInput(connector_with_customer_ref($this->customerRef, $body));
    }

    public function sync(array $params = []): array
    {
        return $this->connector->advanced->sync(connector_with_customer_ref($this->customerRef, $params));
    }
}

class ConnectorDocuments
{
    public function __construct(protected Connector $connector, protected ?string $customerRef = null)
    {
    }

    public function get(string $documentId): array
    {
        return $this->connector->getDocument($documentId, $this->customerRef);
    }

    public function respond(string $documentId, string $customerRef, array $body): array
    {
        return $this->connector->respondDocument($documentId, $customerRef, $body);
    }

    public function ubl(string $documentId): string
    {
        return $this->connector->getDocumentUbl($documentId, $this->customerRef);
    }

    public function evidence(string $documentId): array
    {
        return $this->connector->getDocumentEvidence($documentId, $this->customerRef);
    }

    public function evidenceBundle(string $documentId): array
    {
        return $this->connector->getDocumentEvidenceBundle($documentId, $this->customerRef);
    }

    public function supportPacket(string $documentId): array
    {
        return $this->connector->getDocumentSupportPacket($documentId, $this->customerRef);
    }
}

class ConnectorCustomerDocuments extends ConnectorDocuments
{
    public function __construct(Connector $connector, string $customerRef)
    {
        parent::__construct($connector, $customerRef);
    }

    public function get(string $documentId): array
    {
        return $this->connector->getDocument($documentId, $this->customerRef);
    }

    public function respond(string $documentId, string|array $customerRefOrBody, ?array $body = null): array
    {
        if (is_array($customerRefOrBody)) {
            return $this->connector->respondDocument($documentId, $this->customerRef, $customerRefOrBody);
        }
        if ($body === null) {
            throw new \InvalidArgumentException('Connector response body is required');
        }
        if (connector_trim_string($customerRefOrBody) !== $this->customerRef) {
            throw new \InvalidArgumentException('Connector customerRef conflicts with scoped customer');
        }
        return $this->connector->respondDocument($documentId, $this->customerRef, $body);
    }

    public function ubl(string $documentId): string
    {
        return $this->connector->getDocumentUbl($documentId, $this->customerRef);
    }

    public function evidence(string $documentId): array
    {
        return $this->connector->getDocumentEvidence($documentId, $this->customerRef);
    }

    public function evidenceBundle(string $documentId): array
    {
        return $this->connector->getDocumentEvidenceBundle($documentId, $this->customerRef);
    }

    public function supportPacket(string $documentId): array
    {
        return $this->connector->getDocumentSupportPacket($documentId, $this->customerRef);
    }

    public function send(array $body, ?string $idempotencyKey = null): array
    {
        return $this->connector->submitCustomerDocument($this->customerRef, $body, 'send', $idempotencyKey);
    }

    public function stage(array $body, ?string $idempotencyKey = null): array
    {
        return $this->connector->submitCustomerDocument($this->customerRef, $body, 'stage', $idempotencyKey);
    }

    public function list(array $params = []): array
    {
        return $this->connector->listCustomerDocuments($this->customerRef, $params);
    }

    public function acknowledge(string $documentId, string $reference): array
    {
        return $this->connector->acknowledgeDocument($documentId, $reference, $this->customerRef);
    }

    public function sendDocument(string $documentId): array
    {
        return $this->connector->sendCustomerDocument($documentId, $this->customerRef);
    }

    public function cancelDocument(string $documentId): array
    {
        return $this->connector->cancelCustomerDocument($documentId, $this->customerRef);
    }
}

class ConnectorCustomerEvents
{
    public function __construct(
        private Connector $connector,
        private string $customerRef
    ) {
    }

    public function list(array $params = []): array
    {
        return $this->connector->customerEvents($this->customerRef, $params);
    }
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
