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
    use EPostak\Resources\Box;
    use EPostak\Resources\Connector;
    use EPostak\TokenManager;
    use GuzzleHttp\Client;

    require_once __DIR__ . '/../src/RateLimit.php';
    require_once __DIR__ . '/../src/UblRule.php';
    require_once __DIR__ . '/../src/EPostakError.php';
    require_once __DIR__ . '/../src/UblValidationException.php';
    require_once __DIR__ . '/../src/DuplicateInvoiceNumberError.php';
    require_once __DIR__ . '/../src/TokenManager.php';
    require_once __DIR__ . '/../src/HttpClient.php';
    require_once __DIR__ . '/../src/Resources/Box.php';
    require_once __DIR__ . '/../src/Resources/Connector.php';
    require_once __DIR__ . '/../src/Resources/PeppolDirectory.php';
    require_once __DIR__ . '/../src/Resources/Peppol.php';
    require_once __DIR__ . '/../src/Resources/Sapi.php';

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
        public function __construct(private string $body)
        {
        }

        public function getContents(): string
        {
            return $this->body;
        }
    }

    final class FakeResponse
    {
        public function getStatusCode(): int
        {
            return 200;
        }

        /**
         * @return array<string, list<string>>
         */
        public function getHeaders(): array
        {
            return ['Content-Type' => ['application/json']];
        }

        public function getBody(): FakeBody
        {
            return new FakeBody('{"ok":true}');
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

    function makeConnector(): Connector
    {
        $http = new HttpClient('https://epostak.sk/api/v1', new StaticTokenManager(), 'firm-1', 0);
        return new Connector($http);
    }

    function makeBox(): Box
    {
        $http = new HttpClient('https://epostak.sk/api/v1', new StaticTokenManager(), 'firm-1', 0);
        return new Box($http);
    }

    function fail(string $message): void
    {
        fwrite(STDERR, $message . PHP_EOL);
        exit(1);
    }

    function assertTrue(bool $condition, string $message): void
    {
        if (!$condition) {
            fail($message);
        }
    }

    /**
     * @param array<string, mixed> $headers
     */
    function firmHeader(array $headers): ?string
    {
        foreach ($headers as $name => $value) {
            if (strtolower((string) $name) === 'x-firm-id') {
                return is_array($value) ? (string) ($value[0] ?? '') : (string) $value;
            }
        }
        return null;
    }

    /**
     * @return array{method: string, path: string, options: array}
     */
    function oneRequest(): array
    {
        assertTrue(count(Client::$requests) === 1, 'Expected exactly one HTTP request.');
        return Client::$requests[0];
    }

    function assertRequest(callable $call, string $method, string $path, bool $expectsFirm): void
    {
        Client::$requests = [];
        $call();
        $request = oneRequest();

        assertTrue($request['method'] === $method, "Expected $method {$path}.");
        assertTrue($request['path'] === $path, "Expected path {$path}, got {$request['path']}.");

        $headers = $request['options']['headers'] ?? [];
        $firmHeader = firmHeader($headers);

        if ($expectsFirm) {
            assertTrue($firmHeader === 'firm-1', "Expected X-Firm-Id on {$method} {$path}.");
        } else {
            assertTrue($firmHeader === null, "Did not expect X-Firm-Id on {$method} {$path}.");
        }
    }

    $connector = makeConnector();
    $box = makeBox();

    assertRequest(fn() => $box->list(['status' => 'ready', 'direction' => 'outbound', 'limit' => 10, 'offset' => 5]), 'GET', 'box/items?status=ready&direction=outbound&limit=10&offset=5', true);
    assertRequest(fn() => $box->create([
        'payloadXml' => '<Invoice/>',
        'scheduledFor' => '2026-07-01T00:00:00.000Z',
        'externalId' => 'erp-doc-1',
        'metadata' => ['source' => 'sdk-test'],
    ]), 'POST', 'box/items', true);
    $body = Client::$requests[0]['options']['json'] ?? [];
    assertTrue(($body['payloadXml'] ?? null) === '<Invoice/>', 'Expected Box create payloadXml.');
    assertTrue(($body['externalId'] ?? null) === 'erp-doc-1', 'Expected Box create externalId.');
    assertRequest(fn() => $box->get('box-1'), 'GET', 'box/items/box-1', true);
    assertRequest(fn() => $box->schedule('box-1', '2026-07-01T00:00:00.000Z'), 'POST', 'box/items/box-1/schedule', true);
    $body = Client::$requests[0]['options']['json'] ?? [];
    assertTrue(($body['scheduledFor'] ?? null) === '2026-07-01T00:00:00.000Z', 'Expected Box schedule body.');
    assertRequest(fn() => $box->sendNow('box-1'), 'POST', 'box/items/box-1/send-now', true);
    assertRequest(fn() => $box->retry('box-1'), 'POST', 'box/items/box-1/retry', true);
    assertRequest(fn() => $box->cancel('box-1'), 'POST', 'box/items/box-1/cancel', true);

    assertRequest(fn() => $connector->mapper(['templateKey' => 'pohoda-csv-v1', 'sourceType' => 'csv', 'sourceText' => 'Doklad']), 'POST', 'connector/mapper', false);
    assertRequest(fn() => $connector->zenInput(['customerRef' => 'erp-customer-1']), 'POST', 'connector/zen-input', false);
    assertRequest(fn() => $connector->autopilot(['customerRef' => 'erp-customer-1']), 'POST', 'connector/autopilot', false);
    assertRequest(fn() => $connector->getAutopilotRun('auto-1'), 'GET', 'connector/autopilot/auto-1', false);
    assertRequest(fn() => $connector->sendAutopilotRun('auto-1'), 'POST', 'connector/autopilot/auto-1/send', false);
    assertRequest(fn() => $connector->reconcile(['status' => 'exceptions']), 'GET', 'connector/reconcile?status=exceptions', false);
    assertRequest(fn() => $connector->mailboxes(), 'GET', 'connector/mailbox', false);
    assertRequest(fn() => $connector->repairMailbox(['customerRef' => 'erp-customer-1']), 'POST', 'connector/mailbox/repair', false);
    assertRequest(fn() => $connector->updateMailboxSendPolicy('erp-customer-1', ['policy' => 'daily_batch']), 'PATCH', 'connector/mailbox/erp-customer-1/send-policy', false);
    assertRequest(fn() => $connector->sync(['customerRef' => 'erp-customer-1', 'limit' => 50]), 'GET', 'connector/sync?customerRef=erp-customer-1&limit=50', false);
    assertRequest(fn() => $connector->getDocument('doc-1'), 'GET', 'connector/documents/doc-1', false);
    assertRequest(fn() => $connector->getDocumentUbl('doc-1'), 'GET', 'connector/documents/doc-1/ubl', false);
    assertRequest(fn() => $connector->getDocumentEvidence('doc-1'), 'GET', 'connector/documents/doc-1/evidence', false);
    assertRequest(fn() => $connector->getDocumentEvidenceBundle('doc-1'), 'GET', 'connector/documents/doc-1/evidence-bundle', false);
    assertRequest(fn() => $connector->runAction('action-1', ['note' => 'send now']), 'POST', 'connector/actions/action-1', false);

    assertRequest(fn() => $connector->preflight(['invoiceNumber' => 'FA-1']), 'POST', 'connector/preflight', true);
    assertRequest(fn() => $connector->send(['invoiceNumber' => 'FA-1']), 'POST', 'connector/send', true);
    assertRequest(fn() => $connector->inbox(['limit' => 20]), 'GET', 'connector/inbox?limit=20', true);
    assertRequest(fn() => $connector->events(['limit' => 20]), 'GET', 'connector/events?limit=20', true);

    assertRequest(
        fn() => $connector->customers->for('erp-customer-1')->submitDocument([
            'externalId' => 'FA-1',
            'idempotencyKey' => 'erp-fa-1',
            'payload' => ['invoiceNumber' => 'FA-1'],
        ]),
        'POST',
        'connector/autopilot',
        false
    );
    $body = Client::$requests[0]['options']['json'] ?? [];
    assertTrue(($body['customerRef'] ?? null) === 'erp-customer-1', 'Expected customerRef to be injected.');
    assertTrue(($body['mode'] ?? null) === 'stage', 'Expected submitDocument to default to stage mode.');

    assertRequest(
        fn() => $connector->customers->for('erp-customer-1')->mapper([
            'templateKey' => 'pohoda-csv-v1',
            'sourceType' => 'csv',
            'sourceText' => 'Doklad',
        ]),
        'POST',
        'connector/mapper',
        false
    );
    $body = Client::$requests[0]['options']['json'] ?? [];
    assertTrue(($body['customerRef'] ?? null) === 'erp-customer-1', 'Expected mapper customerRef to be injected.');

    $sapi = new \EPostak\Resources\Sapi(new HttpClient('https://dev.epostak.sk/api/v1', new StaticTokenManager(), 'firm-1', 0));
    Client::$requests = [];
    $sapi->participants->for('0245:1234567890')->documents->send(['xml' => '<Invoice/>'], 'sapi-fa-1');
    $request = oneRequest();
    assertTrue($request['method'] === 'POST', 'Expected SAPI send to POST.');
    assertTrue($request['path'] === 'sapi/v1/document/send', "Expected SAPI send path, got {$request['path']}.");
    $headers = $request['options']['headers'] ?? [];
    assertTrue(($headers['X-Peppol-Participant-Id'] ?? null) === '0245:1234567890', 'Expected SAPI participant header.');
    assertTrue(($headers['Idempotency-Key'] ?? null) === 'sapi-fa-1', 'Expected SAPI idempotency header.');

    $peppol = new \EPostak\Resources\Peppol(new HttpClient('https://dev.epostak.sk/api/v1', new StaticTokenManager(), 'firm-1', 0));
    Client::$requests = [];
    $peppol->capabilities('0245', '2020305606', 'urn:invoice');
    $request = oneRequest();
    assertTrue($request['method'] === 'POST', 'Expected Peppol capabilities to POST.');
    assertTrue($request['path'] === 'peppol/capabilities', "Expected Peppol capabilities path, got {$request['path']}.");
    $body = $request['options']['json'] ?? [];
    assertTrue(($body['participant']['scheme'] ?? null) === '0245', 'Expected nested participant.scheme.');
    assertTrue(($body['participant']['identifier'] ?? null) === '2020305606', 'Expected nested participant.identifier.');
    assertTrue(($body['documentType'] ?? null) === 'urn:invoice', 'Expected documentType to be forwarded.');

    echo "connector_no_firm_id_test passed" . PHP_EOL;
}
