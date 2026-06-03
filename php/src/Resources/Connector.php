<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Connector workflow endpoints for ERP teams.
 *
 * Connector is a polling-first workflow over the Enterprise API. It uses the
 * same credentials, firm scoping, and documentId as the full Enterprise API.
 */
class Connector
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
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
}
