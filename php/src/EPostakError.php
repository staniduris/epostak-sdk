<?php

declare(strict_types=1);

namespace EPostak;

/**
 * Exception thrown when the ePošťák API returns an error response.
 *
 * Wraps the HTTP status code, machine-readable error code, and optional
 * details (e.g. validation errors) returned by the API.
 */
class EPostakError extends \Exception
{
    private int $status;
    private ?string $errorCode;
    private mixed $details;

    /**
     * @param int   $status HTTP status code (0 for network errors).
     * @param array $body   Decoded JSON response body from the API.
     */
    public function __construct(int $status, array $body)
    {
        $error = $body['error'] ?? null;

        if (is_string($error)) {
            $msg = $error;
        } elseif (is_array($error) && isset($error['message'])) {
            $msg = (string) $error['message'];
        } else {
            $msg = 'API request failed';
        }

        parent::__construct($msg);

        $this->status = $status;
        $this->errorCode = null;
        $this->details = null;

        if (is_array($error)) {
            if (isset($error['code'])) {
                $this->errorCode = (string) $error['code'];
            }
            if (isset($error['details'])) {
                $this->details = $error['details'];
            }
        }
    }

    /**
     * Get the HTTP status code of the failed request.
     *
     * @return int HTTP status code (e.g. 400, 401, 404, 500). Returns 0 for network-level errors.
     */
    public function getStatus(): int
    {
        return $this->status;
    }

    /**
     * Get the machine-readable error code from the API, if provided.
     *
     * @return string|null Error code string (e.g. 'VALIDATION_ERROR', 'NOT_FOUND'), or null.
     */
    public function getErrorCode(): ?string
    {
        return $this->errorCode;
    }

    /**
     * Get additional error details from the API, if provided.
     *
     * @return mixed Typically an array of validation errors, or null if no details were returned.
     */
    public function getDetails(): mixed
    {
        return $this->details;
    }
}
