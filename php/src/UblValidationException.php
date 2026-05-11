<?php

declare(strict_types=1);

namespace EPostak;

/**
 * Thrown when the API rejects a document with a UBL validation error (HTTP 422,
 * code `UBL_VALIDATION_ERROR`).
 *
 * The `$rule` property contains the machine-readable rule code. Use
 * {@see UblRule} constants for comparisons.
 *
 * @example
 *   try {
 *       $client->documents->send([...]);
 *   } catch (UblValidationException $e) {
 *       if ($e->rule === UblRule::MISSING_MANDATORY_ELEMENT) {
 *           // Missing required field
 *       }
 *       echo $e->rule . ': ' . $e->getMessage();
 *   }
 */
class UblValidationException extends EPostakError
{
    /** Machine-readable UBL validation rule code (one of {@see UblRule} constants). */
    public string $rule;

    /** Server-assigned request ID from the response, if available. */
    public ?string $requestId;

    /**
     * @param int                              $status  HTTP status code (422).
     * @param array<string, mixed>             $body    Decoded JSON response body.
     * @param array<string, array<int,string>> $headers Optional response headers.
     */
    public function __construct(int $status, array $body, array $headers = [])
    {
        parent::__construct($status, $body, $headers);

        $error = $body['error'] ?? null;
        $rule = null;

        if (is_array($error) && isset($error['rule'])) {
            $rule = (string) $error['rule'];
        } elseif (is_array($error) && isset($error['ruleCode'])) {
            $rule = (string) $error['ruleCode'];
        } elseif (isset($body['rule']) && is_string($body['rule'])) {
            $rule = $body['rule'];
        }

        $this->rule = $rule ?? UblRule::SCHEMA_VIOLATION;
        $this->requestId = $this->getRequestId();
    }
}
