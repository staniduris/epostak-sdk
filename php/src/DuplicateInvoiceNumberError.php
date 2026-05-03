<?php

declare(strict_types=1);

namespace EPostak;

/**
 * Thrown when POST /api/v1/documents/send (or the dashboard create endpoint)
 * rejects an outbound invoice whose invoice_number is already in use for the
 * firm.
 *
 * The conflict key is (firmId, invoiceNumber) — recipient is intentionally
 * NOT part of it; outbound numbering belongs to the sender.
 *
 * Example:
 *
 *     try {
 *         $client->documents->send([...]);
 *     } catch (DuplicateInvoiceNumberError $e) {
 *         $existing = $e->getExistingDocument();
 *         if ($existing !== null) {
 *             error_log("Already sent on {$existing['sentAt']}, id={$existing['id']}");
 *         }
 *     }
 */
class DuplicateInvoiceNumberError extends EPostakError
{
    /** @var list<string> Always ["firmId", "invoiceNumber"]. */
    private array $conflictKey;

    /**
     * The pre-existing outbound invoice that triggered the conflict, or
     * null if it was deleted between the constraint hit and the
     * server-side lookup.
     *
     * Shape (when non-null):
     *   id: string,
     *   invoiceNumber: string,
     *   status: string,
     *   sentAt: string (ISO 8601),
     *   recipient: array{peppolId: ?string, ico: ?string, name: ?string} | null
     *
     * @var array<string, mixed>|null
     */
    private ?array $existingDocument;

    /**
     * @param int                              $status  HTTP status code.
     * @param array<string, mixed>             $body    Decoded JSON response body.
     * @param array<string, array<int,string>> $headers Optional response headers.
     */
    public function __construct(int $status, array $body, array $headers = [])
    {
        parent::__construct($status, $body, $headers);

        $error = $body['error'] ?? null;
        if (!is_array($error)) {
            $error = [];
        }

        $ck = $error['conflictKey'] ?? null;
        $this->conflictKey = is_array($ck)
            ? array_values(array_map('strval', $ck))
            : ['firmId', 'invoiceNumber'];

        $ed = $error['existingDocument'] ?? null;
        if (is_array($ed)) {
            $recipient = null;
            if (isset($ed['recipient']) && is_array($ed['recipient'])) {
                $r = $ed['recipient'];
                $recipient = [
                    'peppolId' => isset($r['peppolId']) ? (string) $r['peppolId'] : null,
                    'ico'      => isset($r['ico']) ? (string) $r['ico'] : null,
                    'name'     => isset($r['name']) ? (string) $r['name'] : null,
                ];
            }
            $this->existingDocument = [
                'id'            => (string) ($ed['id'] ?? ''),
                'invoiceNumber' => (string) ($ed['invoiceNumber'] ?? ''),
                'status'        => (string) ($ed['status'] ?? ''),
                'sentAt'        => (string) ($ed['sentAt'] ?? ''),
                'recipient'     => $recipient,
            ];
        } else {
            $this->existingDocument = null;
        }
    }

    /** @return list<string> */
    public function getConflictKey(): array
    {
        return $this->conflictKey;
    }

    /** @return array<string, mixed>|null */
    public function getExistingDocument(): ?array
    {
        return $this->existingDocument;
    }
}
