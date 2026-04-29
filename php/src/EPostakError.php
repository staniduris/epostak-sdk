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
 * - `getRequiredScope()` — set when the server returns 403 with a
 *   `WWW-Authenticate: Bearer error="insufficient_scope" scope="..."` header.
 */
class EPostakError extends \Exception
{
    private int $status;
    private ?string $errorCode;
    private mixed $details;
    private ?string $requestId;
    private ?string $type;
    private ?string $title;
    private ?string $detail;
    private ?string $instance;
    private ?string $requiredScope;

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
        $this->type = null;
        $this->title = null;
        $this->detail = null;
        $this->instance = null;
        $this->requiredScope = null;

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
