<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * AI-powered OCR extraction from PDFs and images.
 *
 * Extracts structured invoice/document data from uploaded files.
 * Access via `$client->extract`.
 */
class Extract
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * Extract structured data from a single file (AI OCR).
     *
     * Uploads a PDF or image and returns structured invoice data
     * (supplier, customer, line items, totals, dates).
     *
     * @param string      $filePath Absolute path to the file on disk.
     * @param string      $mimeType MIME type: 'application/pdf', 'image/jpeg', 'image/png', or 'image/webp'.
     * @param string|null $fileName Override the filename sent to the API (defaults to basename of filePath).
     * @return array Extracted document data with confidence scores.
     * @throws EPostakError On API error.
     *
     * @example
     *   $data = $client->extract->single('/tmp/invoice.pdf', 'application/pdf');
     *   echo $data['invoiceNumber'] . ' - ' . $data['totalAmount'];
     */
    public function single(string $filePath, string $mimeType, ?string $fileName = null): array
    {
        $multipart = [
            [
                'name' => 'file',
                'contents' => fopen($filePath, 'r'),
                'filename' => $fileName ?? basename($filePath),
                'headers' => ['Content-Type' => $mimeType],
            ],
        ];

        return $this->http->requestMultipart('POST', '/extract', $multipart);
    }

    /**
     * Batch extract structured data from multiple files.
     *
     * @param array<array{filePath: string, mimeType: string, fileName?: string}> $files Up to 10 files.
     *   Each entry requires `filePath` and `mimeType`; `fileName` is optional.
     * @return array Array of extraction results, one per uploaded file.
     * @throws EPostakError On API error.
     */
    public function batch(array $files): array
    {
        $multipart = [];

        foreach ($files as $file) {
            $multipart[] = [
                'name' => 'files',
                'contents' => fopen($file['filePath'], 'r'),
                'filename' => $file['fileName'] ?? basename($file['filePath']),
                'headers' => ['Content-Type' => $file['mimeType']],
            ];
        }

        return $this->http->requestMultipart('POST', '/extract/batch', $multipart);
    }
}
