<?php

declare(strict_types=1);

namespace EPostak;

/**
 * Exception thrown when the ePošťák API returns an error response.
 *
 * The SDK normalizes both the legacy `{ error: { code, message, details } }`
 * envelope and the RFC 7807 `application/problem+json` envelope
 * (`{ type, title, status, detail, instance }`) into the same shape.
 *
 * - `getMessage()`     — human-readable error message.
 * - `getStatus()`      — HTTP status code. `0` indicates a network error.
 * - `getErrorCode()`   — machine-readable error code.
 * - `getDetails()`     — optional additional payload (validation errors, etc.).
 * - `getRequestId()`   — server-assigned request identifier (X-Request-Id).
 * - `getType()`,       — RFC 7807 fields, populated when the server returns
 *   `getTitle()`,        a problem+json envelope.
 *   `getDetail()`,
 *   `getInstance()`
 * - `getApiCode()`, `getMessageSk()`, `getFields()`, `getRule()`,
 *   `getFixHint()`, `getFixHintSk()`, `getDocs()` — the Enterprise API
 *   error behind a Connector answer (error.code keeps the Connector
 *   category), read from `error.apiCode`, `error.message_sk`,
 *   `error.fields`, `error.rule`, `error.fix_hint`, `error.fix_hint_sk`
 *   and `error.docs`. `null` when the API does not send them.
 * - `getRequiredScope()` — set when the server returns 403 with a
 *   `WWW-Authenticate: Bearer error="insufficient_scope" scope="..."` header.
 */
class EPostakError extends \Exception
{
    private int $status;
    private ?string $errorCode;
    private mixed $details;
    private ?string $requestId;
    private ?string $field;
    private ?string $nextAction;
    private ?bool $retryable;
    private ?int $retryAfter;
    private ?string $type;
    private ?string $title;
    private ?string $detail;
    private ?string $instance;
    private ?string $requiredScope;
    private ?string $apiCode;
    private ?string $messageSk;
    /** @var list<array<string, mixed>>|null */
    private ?array $fields;
    private ?string $rule;
    private ?string $fixHint;
    private ?string $fixHintSk;
    private ?string $docs;

    /**
     * @param int                              $status  HTTP status code (0 for network errors).
     * @param array                            $body    Decoded JSON response body from the API.
     * @param array<string, array<int,string>> $headers Optional response headers from Guzzle
     *                                                  (`->getHeaders()` shape: name => string[]).
     *                                                  Used to extract `X-Request-Id` and parse
     *                                                  `WWW-Authenticate` for `requiredScope`.
     */
    public function __construct(int $status, array $body, array $headers = [])
    {
        $this->status = $status;
        $this->errorCode = null;
        $this->details = null;
        $this->requestId = null;
        $this->field = null;
        $this->nextAction = null;
        $this->retryable = null;
        $this->retryAfter = null;
        $this->type = null;
        $this->title = null;
        $this->detail = null;
        $this->instance = null;
        $this->requiredScope = null;
        $this->apiCode = null;
        $this->messageSk = null;
        $this->fields = null;
        $this->rule = null;
        $this->fixHint = null;
        $this->fixHintSk = null;
        $this->docs = null;

        $isProblem =
            (isset($body['title']) || isset($body['detail']))
            && !isset($body['error']);

        $msg = 'API request failed';

        if ($isProblem) {
            if (isset($body['title']) && is_string($body['title'])) {
                $msg = $body['title'];
            } elseif (isset($body['detail']) && is_string($body['detail'])) {
                $msg = $body['detail'];
            }
            if (isset($body['code']) && is_string($body['code'])) {
                $this->errorCode = $body['code'];
            }
            if (isset($body['errors'])) {
                $this->details = $body['errors'];
            }
        } else {
            $error = $body['error'] ?? null;
            if (is_string($error)) {
                $msg = $error;
            } elseif (is_array($error) && isset($error['message'])) {
                $msg = (string) $error['message'];
            } elseif (isset($body['message']) && is_string($body['message'])) {
                $msg = $body['message'];
            }

            if (is_array($error)) {
                if (isset($error['code'])) {
                    $this->errorCode = (string) $error['code'];
                }
                if (isset($error['details'])) {
                    $this->details = $error['details'];
                }
            }
        }

        parent::__construct($msg);

        // RFC 7807 fields — copied through verbatim when present.
        if (isset($body['type']) && is_string($body['type'])) {
            $this->type = $body['type'];
        }
        if (isset($body['title']) && is_string($body['title'])) {
            $this->title = $body['title'];
        }
        if (isset($body['detail']) && is_string($body['detail'])) {
            $this->detail = $body['detail'];
        }
        if (isset($body['instance']) && is_string($body['instance'])) {
            $this->instance = $body['instance'];
        }

        // Server-assigned request ID — body wins, then header.
        if (isset($body['requestId']) && is_string($body['requestId'])) {
            $this->requestId = $body['requestId'];
        } elseif (
            isset($body['error']) && is_array($body['error'])
            && isset($body['error']['requestId']) && is_string($body['error']['requestId'])
        ) {
            $this->requestId = $body['error']['requestId'];
        }
        if ($this->requestId === null) {
            $headerId = $this->headerLine($headers, 'x-request-id');
            if ($headerId !== null) {
                $this->requestId = $headerId;
            }
        }

        // Canonical nested business-error metadata.
        $error = $body['error'] ?? null;
        if (is_array($error)) {
            if (isset($error['field']) && is_string($error['field'])) {
                $this->field = $error['field'];
            }
            $nextAction = $error['nextAction'] ?? $error['next_action'] ?? null;
            if (is_string($nextAction)) {
                $this->nextAction = $nextAction;
            }
            if (array_key_exists('retryable', $error) && is_bool($error['retryable'])) {
                $this->retryable = $error['retryable'];
            }
            $this->apiCode = self::stringOrNull($error['apiCode'] ?? null);
            $this->messageSk = self::stringOrNull($error['message_sk'] ?? null);
            $this->rule = self::stringOrNull($error['rule'] ?? null);
            $this->fixHint = self::stringOrNull($error['fix_hint'] ?? null);
            $this->fixHintSk = self::stringOrNull($error['fix_hint_sk'] ?? null);
            $this->docs = self::stringOrNull($error['docs'] ?? null);
            if (isset($error['fields']) && is_array($error['fields']) && array_is_list($error['fields'])) {
                $this->fields = array_values(array_filter($error['fields'], 'is_array'));
            }
        }
        $retryAfter = $this->headerLine($headers, 'retry-after');
        if ($retryAfter !== null && ctype_digit(trim($retryAfter))) {
            $this->retryAfter = (int) trim($retryAfter);
        }

        // Parse WWW-Authenticate for OAuth `insufficient_scope` rejections.
        $this->requiredScope = $this->parseRequiredScope($headers);
        if ($this->requiredScope === null) {
            $bodyScope = $body['required_scope']
                ?? (isset($body['error']) && is_array($body['error'])
                    ? ($body['error']['required_scope'] ?? null)
                    : null);
            if (is_string($bodyScope) && $bodyScope !== '') {
                $this->requiredScope = $bodyScope;
            }
        }
    }

    public function getStatus(): int
    {
        return $this->status;
    }

    public function getErrorCode(): ?string
    {
        return $this->errorCode;
    }

    public function getDetails(): mixed
    {
        return $this->details;
    }

    public function getRequestId(): ?string
    {
        return $this->requestId;
    }

    public function getField(): ?string
    {
        return $this->field;
    }

    public function getNextAction(): ?string
    {
        return $this->nextAction;
    }

    public function isRetryable(): ?bool
    {
        return $this->retryable;
    }

    public function getRetryAfter(): ?int
    {
        return $this->retryAfter;
    }

    public function getType(): ?string
    {
        return $this->type;
    }

    public function getTitle(): ?string
    {
        return $this->title;
    }

    public function getDetail(): ?string
    {
        return $this->detail;
    }

    public function getInstance(): ?string
    {
        return $this->instance;
    }

    public function getRequiredScope(): ?string
    {
        return $this->requiredScope;
    }

    /** Enterprise API error code behind a Connector error (error.apiCode). */
    public function getApiCode(): ?string
    {
        return $this->apiCode;
    }

    /** Slovak error message (error.message_sk). */
    public function getMessageSk(): ?string
    {
        return $this->messageSk;
    }

    /**
     * Offending fields (error.fields), each as sent by the API, for example
     * `['path' => 'items[0].unitPrice', 'message' => ..., 'message_sk' => ...]`.
     *
     * @return list<array<string, mixed>>|null
     */
    public function getFields(): ?array
    {
        return $this->fields;
    }

    /** Validation rule that rejected the document, e.g. PEPPOL-EN16931-R003 (error.rule). */
    public function getRule(): ?string
    {
        return $this->rule;
    }

    /** How to fix the request (error.fix_hint). */
    public function getFixHint(): ?string
    {
        return $this->fixHint;
    }

    /** How to fix the request, in Slovak (error.fix_hint_sk). */
    public function getFixHintSk(): ?string
    {
        return $this->fixHintSk;
    }

    /** Documentation link for this error (error.docs). */
    public function getDocs(): ?string
    {
        return $this->docs;
    }

    private static function stringOrNull(mixed $value): ?string
    {
        return is_string($value) && $value !== '' ? $value : null;
    }

    /**
     * Look up a header value (case-insensitive) from a Guzzle headers array.
     *
     * @param array<string, array<int, string>> $headers
     */
    private function headerLine(array $headers, string $name): ?string
    {
        $needle = strtolower($name);
        foreach ($headers as $key => $values) {
            if (strtolower((string) $key) === $needle) {
                if (is_array($values) && isset($values[0])) {
                    return (string) $values[0];
                }
                if (is_string($values)) {
                    return $values;
                }
            }
        }
        return null;
    }

    /**
     * Extract the `scope="..."` value from a
     * `WWW-Authenticate: Bearer error="insufficient_scope" scope="documents:send"`
     * header. Returns `null` if the header is absent or the rejection was for
     * a different reason.
     *
     * @param array<string, array<int, string>> $headers
     */
    private function parseRequiredScope(array $headers): ?string
    {
        $raw = $this->headerLine($headers, 'www-authenticate');
        if ($raw === null) {
            return null;
        }
        if (!preg_match('/error\s*=\s*"?insufficient_scope/i', $raw)) {
            return null;
        }
        if (preg_match('/scope\s*=\s*"([^"]+)"/i', $raw, $m)) {
            return $m[1];
        }
        return null;
    }
}
