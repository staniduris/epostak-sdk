<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;

class Payloads
{
    public function __construct(private HttpClient $http)
    {
    }

    public function extract(string $filePath, string $mimeType, ?string $fileName = null): array
    {
        return $this->http->requestMultipart('POST', '/payloads/extract', [[
            'name' => 'file',
            'contents' => fopen($filePath, 'r'),
            'filename' => $fileName ?? basename($filePath),
            'headers' => ['Content-Type' => $mimeType],
        ]]);
    }

    public function extractBatch(array $files): array
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
        return $this->http->requestMultipart('POST', '/payloads/extract/batch', $multipart);
    }

    public function parse(string $xml): array
    {
        return $this->http->request('POST', '/payloads/parse', [
            'json' => ['xml' => $xml],
        ]);
    }

    public function convert(string $inputFormat, string $outputFormat, array|string $document): array
    {
        return $this->http->request('POST', '/payloads/convert', [
            'json' => [
                'input_format' => $inputFormat,
                'output_format' => $outputFormat,
                'document' => $document,
            ],
        ]);
    }

    public function validate(array $body): array
    {
        return $this->http->request('POST', '/payloads/validate', [
            'json' => $body,
        ]);
    }
}
