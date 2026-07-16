<?php

declare(strict_types=1);

namespace GuzzleHttp\Exception {
    interface GuzzleException extends \Throwable
    {
    }
}

namespace GuzzleHttp {
    class Client
    {
        /** @var list<array{method: string, path: string, options: array}> */
        public static array $requests = [];

        public function __construct(array $config = [])
        {
        }

        public function request(string $method, string $path, array $options = []): \EPostak\Tests\FakeResponse
        {
            self::$requests[] = [
                'method' => strtoupper($method),
                'path' => $path,
                'options' => $options,
            ];

            return new \EPostak\Tests\FakeResponse();
        }
    }
}

namespace EPostak\Tests {
    use EPostak\HttpClient;
    use EPostak\Resources\Inbound;
    use EPostak\TokenManager;
    use GuzzleHttp\Client;

    require_once __DIR__ . '/../src/RateLimit.php';
    require_once __DIR__ . '/../src/UblRule.php';
    require_once __DIR__ . '/../src/EPostakError.php';
    require_once __DIR__ . '/../src/UblValidationException.php';
    require_once __DIR__ . '/../src/DuplicateInvoiceNumberError.php';
    require_once __DIR__ . '/../src/TokenManager.php';
    require_once __DIR__ . '/../src/HttpClient.php';
    require_once __DIR__ . '/../src/Resources/Inbound.php';

    final class StaticTokenManager extends TokenManager
    {
        public function __construct()
        {
        }

        public function getAccessToken(): string
        {
            return 'test-token';
        }
    }

    final class FakeBody
    {
        public function getContents(): string
        {
            return '{"documents":[],"next_cursor":null,"has_more":false}';
        }
    }

    final class FakeResponse
    {
        public function getStatusCode(): int
        {
            return 200;
        }

        /** @return array<string, list<string>> */
        public function getHeaders(): array
        {
            return ['Content-Type' => ['application/json']];
        }

        public function getBody(): FakeBody
        {
            return new FakeBody();
        }

        public function getReasonPhrase(): string
        {
            return 'OK';
        }

        public function hasHeader(string $name): bool
        {
            return false;
        }

        public function getHeaderLine(string $name): string
        {
            return '';
        }
    }

    function fail(string $message): void
    {
        fwrite(STDERR, $message . PHP_EOL);
        exit(1);
    }

    function assertPath(string $expected, array $params): void
    {
        Client::$requests = [];
        $http = new HttpClient(
            'https://dev.epostak.sk/api/v1',
            new StaticTokenManager(),
            null,
            0,
        );

        (new Inbound($http))->list($params);
        $path = Client::$requests[0]['path'] ?? null;
        if ($path !== $expected) {
            fail("Expected path {$expected}, got " . (string) $path);
        }
    }

    assertPath(
        'inbound/documents?since=cursor-from-response&limit=50',
        ['since' => 'cursor-from-response', 'limit' => 50],
    );
    assertPath(
        'inbound/documents?since=legacy-cursor',
        ['next_cursor' => 'legacy-cursor'],
    );
    assertPath(
        'inbound/documents?since=canonical-cursor',
        ['since' => 'canonical-cursor', 'next_cursor' => 'legacy-cursor'],
    );

    fwrite(STDOUT, "php inbound pagination tests passed\n");
}
