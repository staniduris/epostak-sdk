<?php

declare(strict_types=1);

namespace GuzzleHttp\Exception {
    interface GuzzleException extends \Throwable
    {
    }

    final class ConnectException extends \RuntimeException implements GuzzleException
    {
    }
}

namespace GuzzleHttp {
    class Client
    {
        /** @var list<array{method: string, path: string, options: array}> */
        public static array $requests = [];

        /** @var list<\EPostak\Tests\FakeResponse> */
        public static array $responses = [];

        /** @var list<\Throwable> */
        public static array $failures = [];

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

            $failure = array_shift(self::$failures);
            if ($failure !== null) {
                throw $failure;
            }

            return array_shift(self::$responses) ?? new \EPostak\Tests\FakeResponse();
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

    $retryableError = new \EPostak\EPostakError(409, [
        'error' => [
            'code' => 'idempotency_in_flight',
            'message' => 'Still processing',
            'field' => 'externalId',
            'nextAction' => 'retry',
            'retryable' => true,
            'requestId' => 'req-body',
        ],
    ], ['Retry-After' => ['7'], 'X-Request-Id' => ['req-header']]);
    assertTrue($retryableError->getField() === 'externalId', 'Expected error field metadata.');
    assertTrue($retryableError->getNextAction() === 'retry', 'Expected nextAction metadata.');
    assertTrue($retryableError->isRetryable() === true, 'Expected retryable metadata.');
    assertTrue($retryableError->getRequestId() === 'req-body', 'Expected nested requestId to win.');
    assertTrue($retryableError->getRetryAfter() === 7, 'Expected Retry-After delta seconds.');
    $validationError = new \EPostak\EPostakError(422, [
        'error' => ['code' => 'validation_failed', 'message' => 'Fix request', 'retryable' => false],
    ]);
    assertTrue($validationError->isRetryable() === false, 'Expected non-retryable validation metadata.');
    assertTrue($validationError->getRetryAfter() === null, 'Expected absent Retry-After metadata.');

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
        /**
         * @param array<string, list<string>> $headers
         */
        public function __construct(
            private int $status = 200,
            private string $body = '{"ok":true}',
            private array $headers = ['Content-Type' => ['application/json']],
            private string $reason = 'OK'
        ) {
        }

        public function getStatusCode(): int
        {
            return $this->status;
        }

        /**
         * @return array<string, list<string>>
         */
        public function getHeaders(): array
        {
            return $this->headers;
        }

        public function getBody(): FakeBody
        {
            return new FakeBody($this->body);
        }

        public function getReasonPhrase(): string
        {
            return $this->reason;
        }

        public function hasHeader(string $name): bool
        {
            foreach ($this->headers as $headerName => $_values) {
                if (strcasecmp($headerName, $name) === 0) {
                    return true;
                }
            }
            return false;
        }

        public function getHeaderLine(string $name): string
        {
            foreach ($this->headers as $headerName => $values) {
                if (strcasecmp($headerName, $name) === 0) {
                    return implode(', ', $values);
                }
            }
            return '';
        }
    }

    function makeConnector(int $maxRetries = 0): Connector
    {
        $http = new HttpClient('https://epostak.sk/api/v1', new StaticTokenManager(), 'firm-1', $maxRetries);
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

    /**
     * @param array{method: string, path: string, options: array} $request
     * @return array<mixed>
     */
    function requestJsonBody(array $request): array
    {
        if (array_key_exists('json', $request['options'])) {
            return $request['options']['json'];
        }

        $raw = $request['options']['body'] ?? null;
        assertTrue(is_string($raw), 'Expected a JSON request body.');
        $decoded = json_decode($raw, true, 512, JSON_THROW_ON_ERROR);
        assertTrue(is_array($decoded), 'Expected the JSON request body to decode to an array.');
        return $decoded;
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
    assertTrue($connector->advanced->documents === $connector->documents, 'Expected advanced document compatibility surface.');
    $scopedCustomer = $connector->customers->for('erp-customer-1');
    assertTrue($scopedCustomer->mailbox === $scopedCustomer->advanced->mailbox, 'Expected mailbox compatibility alias to remain available.');
    try {
        $connector->customers->for('  ');
        fail('Expected blank Connector customerRef to be rejected.');
    } catch (\InvalidArgumentException $error) {
        assertTrue(str_contains($error->getMessage(), 'customerRef is required'), 'Expected customerRef validation message.');
    }

    Client::$responses = [new FakeResponse(200, '{"webhook":{"id":"wh-1","url":"https://erp.example/hook","events":["document.received"],"active":true,"failedAttempts":0,"createdAt":"2026-07-15T10:00:00Z","updatedAt":"2026-07-15T10:00:00Z"}}')];
    Client::$requests = [];
    $currentWebhook = $connector->webhook->get();
    assertTrue(($currentWebhook['webhook']['id'] ?? null) === 'wh-1', 'Expected Connector webhook envelope.');
    $request = oneRequest();
    assertTrue($request['method'] === 'GET' && $request['path'] === 'connector/webhook', 'Expected Connector webhook GET.');
    assertTrue(firmHeader($request['options']['headers'] ?? []) === null, 'Did not expect X-Firm-Id on webhook GET.');

    Client::$responses = [new FakeResponse(201, '{"webhook":{"id":"wh-1","url":"https://erp.example/hook","events":["document.received"],"active":true,"failedAttempts":0,"createdAt":"2026-07-15T10:00:00Z","updatedAt":"2026-07-15T10:00:00Z"},"secret":"' . str_repeat('a', 64) . '"}')];
    Client::$requests = [];
    $configuredWebhook = $connector->webhook->configure(
        '  https://erp.example/hook  ',
        ['document.received', 'document.delivered']
    );
    assertTrue(($configuredWebhook['secret'] ?? null) === str_repeat('a', 64), 'Expected one-time Connector webhook secret.');
    $request = oneRequest();
    assertTrue($request['method'] === 'PUT' && $request['path'] === 'connector/webhook', 'Expected Connector webhook PUT.');
    assertTrue(firmHeader($request['options']['headers'] ?? []) === null, 'Did not expect X-Firm-Id on webhook PUT.');
    $body = Client::$requests[0]['options']['json'] ?? [];
    assertTrue(($body['url'] ?? null) === 'https://erp.example/hook', 'Expected normalized Connector webhook URL.');
    assertTrue(
        ($body['events'] ?? null) === ['document.received', 'document.delivered'],
        'Expected Connector webhook event filters.'
    );
    assertRequest(fn() => $connector->webhook->rotateSecret(), 'POST', 'connector/webhook/rotate-secret', false);

    Client::$requests = [];
    Client::$responses = [new FakeResponse(
        200,
        '{"deliveryId":"whd-1","status":"queued","event":{"id":"evt-1","type":"document.delivered","customerRef":"erp-customer-1","documentId":"doc-1","state":"delivered","occurredAt":"2026-07-15T10:00:00Z","data":{"customerRef":"erp-customer-1","direction":"outbound","type":"invoice","number":null,"response":null},"test":true}}'
    )];
    $testEvent = $connector->webhook->test("\u{00A0}erp-customer-1\u{FEFF}");
    $request = oneRequest();
    assertTrue($request['method'] === 'POST', 'Expected Connector webhook test to POST.');
    assertTrue($request['path'] === 'connector/webhook/test', 'Expected Connector webhook test path.');
    assertTrue(firmHeader($request['options']['headers'] ?? []) === null, 'Did not expect X-Firm-Id on webhook test.');
    assertTrue(
        ($request['options']['json'] ?? null) === ['customerRef' => 'erp-customer-1'],
        'Expected webhook test body to contain only normalized customerRef.'
    );
    assertTrue(
        ($testEvent['event']['customerRef'] ?? null) === 'erp-customer-1',
        'Expected canonical webhook event to expose root customerRef.'
    );
    assertTrue(($testEvent['deliveryId'] ?? null) === 'whd-1' && ($testEvent['status'] ?? null) === 'queued', 'Expected queued test delivery envelope.');
    assertTrue(($testEvent['event']['data']['customerRef'] ?? null) === 'erp-customer-1', 'Expected nested compatibility customerRef alias.');
    assertTrue(array_key_exists('number', $testEvent['event']['data'] ?? []) && $testEvent['event']['data']['number'] === null, 'Expected nullable event number.');
    assertTrue(array_key_exists('response', $testEvent['event']['data'] ?? []) && $testEvent['event']['data']['response'] === null, 'Expected nullable event response.');
    assertTrue(($testEvent['event']['test'] ?? null) === true, 'Expected test event marker.');

    assertRequest(
        fn() => $connector->webhook->deliveries(['cursor' => 'next', 'limit' => 25, 'status' => 'failed']),
        'GET',
        'connector/webhook/deliveries?cursor=next&limit=25&status=FAILED',
        false
    );
    assertRequest(fn() => $connector->webhook->delete(), 'DELETE', 'connector/webhook', false);

    assertRequest(fn() => $connector->webhook->getDelivery('delivery 1'), 'GET', 'connector/webhook/deliveries/delivery%201', false);
    Client::$requests = [];
    $connector->webhook->replayDelivery('delivery 1', 'replay-key');
    $replayRequest = oneRequest();
    assertTrue($replayRequest['path'] === 'connector/webhook/deliveries/delivery%201/replay', 'Expected Connector replay path.');
    assertTrue(($replayRequest['options']['headers']['Idempotency-Key'] ?? null) === 'replay-key', 'Expected replay Idempotency-Key.');
    assertTrue(firmHeader($replayRequest['options']['headers'] ?? []) === null, 'Did not expect X-Firm-Id on replay.');
    Client::$requests = [];
    $connector->webhook->runTestSuite('erp-acme', 'suite-key');
    $suiteRequest = oneRequest();
    assertTrue($suiteRequest['path'] === 'connector/webhook/test-suite', 'Expected Connector test-suite path.');
    assertTrue(($suiteRequest['options']['headers']['Idempotency-Key'] ?? null) === 'suite-key', 'Expected test-suite Idempotency-Key.');
    assertTrue(firmHeader($suiteRequest['options']['headers'] ?? []) === null, 'Did not expect X-Firm-Id on test-suite.');
    assertRequest(fn() => $connector->webhook->getTestSuite('run 1'), 'GET', 'connector/webhook/test-suite/run%201', false);
    try {
        $connector->webhook->configure('  ');
        fail('Expected blank Connector webhook URL to be rejected.');
    } catch (\InvalidArgumentException $error) {
        assertTrue(str_contains($error->getMessage(), 'webhook URL is required'), 'Expected webhook URL validation message.');
    }
    try {
        $connector->webhook->test('  ');
        fail('Expected blank webhook-test customerRef to be rejected.');
    } catch (\InvalidArgumentException $error) {
        assertTrue(str_contains($error->getMessage(), 'customerRef is required'), 'Expected webhook customerRef validation message.');
    }

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

    assertRequest(fn() => $connector->advanced->mapper(['templateKey' => 'pohoda-csv-v1', 'sourceType' => 'csv', 'sourceText' => 'Doklad']), 'POST', 'connector/mapper', false);
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
    assertRequest(fn() => $connector->advanced->events(['limit' => 20]), 'GET', 'connector/events?limit=20', true);
    assertRequest(fn() => $connector->customers->for('erp-customer-1')->events->list(['limit' => 20]), 'GET', 'connector/events?customerRef=erp-customer-1&limit=20', false);

    assertRequest(
        fn() => $connector->customers->for('erp-customer-1')->submitDocument([
            'externalId' => 'legacy-1',
            'payload' => ['number' => 'FA-1'],
        ]),
        'POST',
        'connector/autopilot',
        false
    );
    $body = Client::$requests[0]['options']['json'] ?? [];
    assertTrue(($body['customerRef'] ?? null) === 'erp-customer-1', 'Expected customerRef to be injected.');
    assertTrue(($body['mode'] ?? null) === 'stage', 'Expected legacy submitDocument to default to staged Autopilot.');

    $documentBody = static fn (string $externalId): array => [
        'externalId' => $externalId,
        'type' => 'invoice',
        'number' => 'FA-1',
        'recipient' => ['country' => 'SK', 'taxId' => '2120123456'],
        'lines' => [['description' => 'Licence', 'quantity' => 1, 'unitPrice' => 1, 'vatRate' => 23]],
    ];

    Client::$requests = [];
    Client::$responses = [
        new FakeResponse(201, '{"id":"doc-stage-1","state":"queued"}'),
        new FakeResponse(200, '{"documents":[],"nextCursor":"cur-2","hasMore":true}'),
        new FakeResponse(
            200,
            '{"id":"doc-in-1","customerRef":"erp-customer-1","response":{"status":"accepted","direction":"sent","delivery":"queued","respondedAt":"2026-07-15T12:00:00Z"},"idempotent":true}'
        ),
    ];
    $documents = $connector->customers->for('erp-customer-1')->documents;
    $mismatchedCustomerRejected = false;
    try {
        $documents->respond('doc-in-1', 'another-customer', ['status' => 'accepted']);
    } catch (\InvalidArgumentException $error) {
        $mismatchedCustomerRejected = str_contains($error->getMessage(), 'customerRef conflicts with scoped customer');
    }
    assertTrue($mismatchedCustomerRejected, 'Expected a scoped Invoice Response to reject a different customerRef.');
    assertTrue(count(Client::$requests) === 0, 'A rejected scoped customerRef must not reach the network.');
    $documents->stage([
        'externalId' => 'FA-STAGE-1',
        'type' => 'invoice',
        'number' => 'FA-STAGE-1',
        'recipient' => ['country' => 'SK', 'taxId' => '2120123456'],
        'lines' => [['description' => 'Licence', 'quantity' => 1, 'unitPrice' => 100, 'vatRate' => 23]],
    ], 'connector-stage-key');
    $page = $documents->list([
        'direction' => 'inbound',
        'state' => 'received',
        'type' => 'invoice',
        'createdAfter' => '2026-07-01T00:00:00Z',
        'cursor' => 'cur-1',
        'limit' => 25,
    ]);
    $invoiceResponse = $documents->respond('doc-in-1', [
        'status' => 'accepted',
        'note' => 'Imported into ERP',
    ]);

    assertTrue(count(Client::$requests) === 3, 'Expected stage, filtered list, and invoice response requests.');
    [$stageRequest, $listRequest, $respondRequest] = Client::$requests;
    assertTrue($stageRequest['method'] === 'POST', 'Expected staged document POST.');
    assertTrue($stageRequest['path'] === 'connector/documents', 'Expected canonical staged document path.');
    assertTrue(($stageRequest['options']['headers']['Idempotency-Key'] ?? null) === 'connector-stage-key', 'Expected staged document idempotency key.');
    assertTrue(firmHeader($stageRequest['options']['headers'] ?? []) === null, 'Did not expect X-Firm-Id on staged document.');
    $stageBody = requestJsonBody($stageRequest);
    assertTrue(($stageBody['customerRef'] ?? null) === 'erp-customer-1', 'Expected stage customerRef.');
    assertTrue(($stageBody['delivery'] ?? null) === 'stage', 'Expected delivery=stage.');
    assertTrue(
        $listRequest['path'] === 'connector/documents?customerRef=erp-customer-1&direction=inbound&state=received&type=invoice&createdAfter=2026-07-01T00%3A00%3A00Z&cursor=cur-1&limit=25',
        "Unexpected filtered Connector list path {$listRequest['path']}."
    );
    assertTrue(firmHeader($listRequest['options']['headers'] ?? []) === null, 'Did not expect X-Firm-Id on filtered list.');
    assertTrue(
        $respondRequest['path'] === 'connector/documents/doc-in-1/respond?customerRef=erp-customer-1',
        'Expected canonical Connector Invoice Response path.'
    );
    assertTrue(requestJsonBody($respondRequest) === [
        'status' => 'accepted',
        'note' => 'Imported into ERP',
    ], 'Expected business status and note only in Invoice Response body.');
    assertTrue(!isset($respondRequest['options']['headers']['Idempotency-Key']), 'Invoice Response must not require a caller idempotency key.');
    assertTrue(firmHeader($respondRequest['options']['headers'] ?? []) === null, 'Did not expect X-Firm-Id on Invoice Response.');
    assertTrue(($page['nextCursor'] ?? null) === 'cur-2' && ($page['hasMore'] ?? false) === true, 'Expected typed list projection.');
    assertTrue(($invoiceResponse['response']['status'] ?? null) === 'accepted', 'Expected accepted Invoice Response status.');
    assertTrue(($invoiceResponse['response']['direction'] ?? null) === 'sent', 'Expected sent Invoice Response direction.');
    assertTrue(($invoiceResponse['response']['delivery'] ?? null) === 'queued', 'Expected queued Invoice Response delivery.');
    assertTrue(($invoiceResponse['idempotent'] ?? false) === true, 'Expected idempotent response projection.');

    $respondRetryConnector = makeConnector(1);
    Client::$requests = [];
    Client::$responses = [
        new FakeResponse(
            503,
            '{"error":{"code":"temporary","message":"retry"}}',
            ['Content-Type' => ['application/json'], 'Retry-After' => ['0']],
            'Service Unavailable'
        ),
        new FakeResponse(
            200,
            '{"id":"doc-in-1","customerRef":"erp-customer-1","response":{"status":"accepted","direction":"sent","delivery":"queued","respondedAt":"2026-07-15T12:00:00Z"},"idempotent":true}'
        ),
    ];
    $respondRetryConnector->customers->for('erp-customer-1')->documents->respond('doc-in-1', [
        'status' => 'accepted',
        'note' => 'Imported into ERP',
    ]);
    assertTrue(count(Client::$requests) === 2, 'Expected server-idempotent Invoice Response to retry once.');
    assertTrue(Client::$requests[0]['path'] === Client::$requests[1]['path'], 'Expected stable Invoice Response retry path.');
    assertTrue(Client::$requests[0]['options']['body'] === Client::$requests[1]['options']['body'], 'Expected byte-identical Invoice Response retry body.');
    foreach (Client::$requests as $retriedResponseRequest) {
        assertTrue(firmHeader($retriedResponseRequest['options']['headers'] ?? []) === null, 'Did not expect X-Firm-Id on retried Invoice Response.');
        assertTrue(!isset($retriedResponseRequest['options']['headers']['Idempotency-Key']), 'Invoice Response retry must not require a caller key.');
    }
    Client::$responses = [];

    Client::$requests = [];
    $connector->customers->for('a:b')->documents->send($documentBody('c'));
    $collisionA = (string) (Client::$requests[0]['options']['headers']['Idempotency-Key'] ?? '');
    Client::$requests = [];
    $connector->customers->for('a')->documents->send($documentBody('b:c'));
    $collisionB = (string) (Client::$requests[0]['options']['headers']['Idempotency-Key'] ?? '');
    Client::$requests = [];
    $connector->customers->for(str_repeat('c', 255))->documents->send($documentBody(str_repeat('e', 255)));
    $maxKey = (string) (Client::$requests[0]['options']['headers']['Idempotency-Key'] ?? '');
    Client::$requests = [];
    $connector->customers->for("\u{00A0}\u{FEFF}zákazník😀\u{FEFF}\u{00A0}")->documents->send($documentBody("\u{FEFF}FA-žltý-1\u{00A0}"));
    $normalizedKey = (string) (Client::$requests[0]['options']['headers']['Idempotency-Key'] ?? '');
    $normalizedBody = requestJsonBody(Client::$requests[0]);
    assertTrue($collisionA === 'connector:v1:540e8f1c5ae653a7d7e2fe88f7eb8dcabea924d661b1542ad191bb1848e0c33d', 'Unexpected collision vector A.');
    assertTrue($collisionB === 'connector:v1:e482a79a788392ccae4952360dd438820641e4c162b4952b42d35e78260d70be', 'Unexpected collision vector B.');
    assertTrue($collisionA !== $collisionB, 'Length-prefixed tuples must not collide.');
    assertTrue($maxKey === 'connector:v1:7182fd43682e0689adf34c908bc3ec162aaf1687c167fdbff714ff43daa4b111', 'Unexpected max-length vector.');
    assertTrue($normalizedKey === 'connector:v1:eec0ca654af898913432fbc7b7441a05080f72099f6d2ff85852f78c7458fdfd', 'Unexpected normalized Unicode vector.');
    assertTrue(($normalizedBody['customerRef'] ?? null) === 'zákazník😀', 'Expected normalized customerRef body field.');
    assertTrue(($normalizedBody['externalId'] ?? null) === 'FA-žltý-1', 'Expected normalized externalId body field.');
    Client::$requests = [];
    $connector->customers->for("\u{0085}zákazník😀\u{0085}")->documents->send($documentBody("\u{0085}FA-žltý-1\u{0085}"));
    $controlKey = (string) (Client::$requests[0]['options']['headers']['Idempotency-Key'] ?? '');
    $controlBody = requestJsonBody(Client::$requests[0]);
    assertTrue($controlKey === 'connector:v1:ff49689a9ece4c0319420ed07fc3a2a5b2e2e7bb6d4430a68557e372fdf70080', 'U+0085 must not be trimmed.');
    assertTrue(($controlBody['customerRef'] ?? null) === "\u{0085}zákazník😀\u{0085}", 'Expected U+0085 customerRef to remain.');
    assertTrue(($controlBody['externalId'] ?? null) === "\u{0085}FA-žltý-1\u{0085}", 'Expected U+0085 externalId to remain.');
    assertTrue(strlen($collisionA) === 77 && strlen($collisionB) === 77 && strlen($maxKey) === 77, 'Expected bounded 77-character keys.');
    Client::$requests = [];
    $connector->customers->for('customer')->documents->send($documentBody('external'), 'caller-key');
    assertTrue((Client::$requests[0]['options']['headers']['Idempotency-Key'] ?? null) === 'caller-key', 'Expected explicit key to remain unchanged.');
    try {
        $connector->customers->for('customer')->documents->send($documentBody('empty-key'), '');
        fail('Expected blank Connector idempotency key to be rejected.');
    } catch (\InvalidArgumentException $error) {
        assertTrue(str_contains($error->getMessage(), 'idempotency key'), 'Expected idempotency key validation message.');
    }

    $retryConnector = makeConnector(1);
    Client::$requests = [];
    Client::$responses = [
        new FakeResponse(
            503,
            '{"error":{"code":"temporary","message":"retry"}}',
            ['Content-Type' => ['application/json'], 'Retry-After' => ['0']],
            'Service Unavailable'
        ),
        new FakeResponse(201, '{"id":"doc-retry","state":"queued"}'),
    ];
    $retryBody = $documentBody('FA-retry');
    $retryBody['metadata'] = ['emptyObject' => (object) []];
    $retryResult = $retryConnector->customers->for('customer')->documents->send($retryBody);
    $retryBody['lines'][0]['description'] = 'Mutated after request';
    assertTrue(($retryResult['id'] ?? null) === 'doc-retry', 'Expected keyed Connector retry to return the successful response.');
    assertTrue(count(Client::$requests) === 2, 'Expected keyed Connector POST to retry once.');
    assertTrue(
        Client::$requests[0]['options']['body'] === Client::$requests[1]['options']['body'],
        'Expected retry attempts to use a byte-identical JSON snapshot.'
    );
    assertTrue(
        (Client::$requests[0]['options']['headers']['Idempotency-Key'] ?? null) ===
            (Client::$requests[1]['options']['headers']['Idempotency-Key'] ?? null),
        'Expected retry attempts to reuse the same idempotency key.'
    );
    assertTrue(
        (requestJsonBody(Client::$requests[0])['lines'][0]['description'] ?? null) === 'Licence',
        'Expected caller mutation not to alter the request snapshot.'
    );
    $retrySnapshot = (string) Client::$requests[0]['options']['body'];
    $retrySnapshotObject = json_decode($retrySnapshot, false, 512, JSON_THROW_ON_ERROR);
    assertTrue(
        ($retrySnapshotObject->metadata->emptyObject ?? null) instanceof \stdClass,
        'Expected nested empty JSON objects to remain objects in the wire snapshot.'
    );
    foreach (Client::$requests as $retryRequest) {
        assertTrue(!array_key_exists('json', $retryRequest['options']), 'Expected a frozen JSON byte body instead of Guzzle re-encoding.');
        assertTrue(
            ($retryRequest['options']['headers']['Content-Type'] ?? null) === 'application/json',
            'Expected application/json on frozen Connector retry bodies.'
        );
    }

    Client::$requests = [];
    Client::$responses = [
        new FakeResponse(503, '{"error":{"code":"temporary","message":"retry"}}', ['Retry-After' => ['0']]),
        new FakeResponse(200, '{"id":"doc-retry","state":"cancelled"}'),
    ];
    $retryConnector->customers->for('customer')->documents->cancelDocument('doc-retry');
    assertTrue(count(Client::$requests) === 2, 'Expected server-idempotent lifecycle POST to retry once.');

    Client::$requests = [];
    Client::$responses = [
        new FakeResponse(503, '{"error":{"code":"temporary","message":"retry"}}', ['Retry-After' => ['0']]),
        new FakeResponse(200, '<Invoice/>', ['Content-Type' => ['application/xml']]),
    ];
    $ubl = $retryConnector->customers->for('customer')->advanced->documents->ubl('doc-retry');
    assertTrue($ubl === '<Invoice/>', 'Expected retried customer UBL response.');
    assertTrue(count(Client::$requests) === 2, 'Expected safe UBL GET to retry once.');
    foreach (Client::$requests as $request) {
        assertTrue(
            $request['path'] === 'connector/documents/doc-retry/ubl?customerRef=customer',
            'Expected customerRef query on every retried UBL GET.'
        );
        assertTrue(
            firmHeader($request['options']['headers'] ?? []) === null,
            'Did not expect X-Firm-Id on retried customer UBL GET.'
        );
    }

    Client::$requests = [];
    Client::$responses = [
        new FakeResponse(
            409,
            '{"error":{"code":"idempotency_in_flight","message":"busy","retryable":true}}',
            ['Content-Type' => ['application/json'], 'Retry-After' => ['0']],
            'Conflict'
        ),
    ];
    try {
        $retryConnector->customers->for('customer')->documents->send($documentBody('FA-conflict'), 'conflict-key');
        fail('Expected Connector 409 to be surfaced.');
    } catch (\EPostak\EPostakError $error) {
        assertTrue($error->getStatus() === 409, 'Expected surfaced Connector 409 status.');
    }
    assertTrue(count(Client::$requests) === 1, 'Expected Connector 409 not to be retried.');

    $transportConnector = makeConnector(1);
    Client::$requests = [];
    Client::$failures = [new \GuzzleHttp\Exception\ConnectException('socket reset')];
    Client::$responses = [new FakeResponse(201, '{"id":"doc-transport","state":"queued"}')];
    $transportConnector->customers->for('erp-customer-1')->documents->stage(
        $documentBody('FA-TRANSPORT-1'),
        'connector-transport-key'
    );
    assertTrue(count(Client::$requests) === 2, 'Expected Connector transport failure to retry once.');
    assertTrue(Client::$requests[0]['path'] === Client::$requests[1]['path'], 'Expected identical Connector retry path.');
    assertTrue(Client::$requests[0]['options']['body'] === Client::$requests[1]['options']['body'], 'Expected byte-identical Connector retry body.');
    assertTrue(
        (Client::$requests[0]['options']['headers']['Idempotency-Key'] ?? null) ===
            (Client::$requests[1]['options']['headers']['Idempotency-Key'] ?? null),
        'Expected identical Connector retry key.'
    );
    assertTrue(
        (Client::$requests[0]['options']['headers']['Idempotency-Key'] ?? null) === 'connector-transport-key',
        'Expected explicit Connector transport retry key.'
    );
    assertTrue(
        (requestJsonBody(Client::$requests[0])['delivery'] ?? null) === 'stage' &&
            (requestJsonBody(Client::$requests[0])['customerRef'] ?? null) === 'erp-customer-1',
        'Expected stable staged Connector payload.'
    );
    foreach (Client::$requests as $transportRequest) {
        assertTrue(firmHeader($transportRequest['options']['headers'] ?? []) === null, 'Did not expect X-Firm-Id on Connector transport retry.');
    }

    $transportSapi = new \EPostak\Resources\Sapi(
        new HttpClient('https://dev.epostak.sk/api/v1', new StaticTokenManager(), 'firm-1', 1)
    );
    Client::$requests = [];
    Client::$failures = [new \GuzzleHttp\Exception\ConnectException('socket reset')];
    Client::$responses = [];
    try {
        $transportSapi->participants->for('0245:1234567890')->documents->send(
            ['xml' => '<Invoice/>'],
            'sapi-transport-key'
        );
        fail('Expected SAPI transport failure to surface.');
    } catch (\EPostak\EPostakError $error) {
        assertTrue($error->getStatus() === 0, 'Expected SAPI transport error status 0.');
    }
    assertTrue(count(Client::$requests) === 1, 'Expected SAPI mutating POST transport failure after one attempt.');
    Client::$failures = [];

    Client::$requests = [];
    Client::$responses = [
        new FakeResponse(
            200,
            '{"events":[{"id":"evt-1","customerRef":"customer","type":"document.cancelled","documentId":"doc-1","state":"cancelled","data":{"customerRef":"customer","direction":"outbound","type":"invoice","number":null,"response":null}}]}'
        ),
    ];
    $eventResult = $connector->customers->for('customer')->events->list();
    assertTrue(
        ($eventResult['events'][0]['type'] ?? null) === 'document.cancelled',
        'Expected document.cancelled business event to be preserved.'
    );
    assertTrue(array_key_exists('response', $eventResult['events'][0]['data'] ?? []) && $eventResult['events'][0]['data']['response'] === null, 'Expected nullable polling response projection.');

    assertRequest(
        fn() => $connector->customers->for('erp-customer-1')->documents->sendDocument('doc-1'),
        'POST',
        'connector/documents/doc-1/send?customerRef=erp-customer-1',
        false
    );
    $options = Client::$requests[0]['options'];
    assertTrue(!array_key_exists('json', $options), 'Expected staged document send to omit the request body.');
    try {
        $connector->customers->for('erp-customer-1')->documents->sendDocument('  ');
        fail('Expected blank Connector documentId to be rejected.');
    } catch (\InvalidArgumentException $error) {
        assertTrue(str_contains($error->getMessage(), 'documentId is required'), 'Expected documentId validation message.');
    }
    assertRequest(
        fn() => $connector->customers->for('erp-customer-1')->documents->cancelDocument('doc-2'),
        'POST',
        'connector/documents/doc-2/cancel?customerRef=erp-customer-1',
        false
    );
    $options = Client::$requests[0]['options'];
    assertTrue(!array_key_exists('json', $options), 'Expected staged document cancel to omit the request body.');

    $customerA = $connector->customers->for('customer A/1');
    $customerBDocument = 'customer-b-doc';
    $customerQuery = '?customerRef=customer+A%2F1';
    assertRequest(fn() => $customerA->documents->get($customerBDocument), 'GET', 'connector/documents/customer-b-doc' . $customerQuery, false);
    assertRequest(
        fn() => $customerA->documents->acknowledge($customerBDocument, 'ERP-ACK-1'),
        'POST',
        'connector/documents/customer-b-doc/acknowledge' . $customerQuery,
        false
    );
    assertTrue(
        requestJsonBody(Client::$requests[0]) === ['reference' => 'ERP-ACK-1'],
        'Expected acknowledge body to contain only the processing reference.'
    );
    assertRequest(fn() => $customerA->documents->sendDocument($customerBDocument), 'POST', 'connector/documents/customer-b-doc/send' . $customerQuery, false);
    assertRequest(fn() => $customerA->documents->cancelDocument($customerBDocument), 'POST', 'connector/documents/customer-b-doc/cancel' . $customerQuery, false);
    assertRequest(fn() => $customerA->advanced->documents->ubl($customerBDocument), 'GET', 'connector/documents/customer-b-doc/ubl' . $customerQuery, false);
    assertRequest(fn() => $customerA->advanced->documents->evidence($customerBDocument), 'GET', 'connector/documents/customer-b-doc/evidence' . $customerQuery, false);
    assertRequest(fn() => $customerA->advanced->documents->evidenceBundle($customerBDocument), 'GET', 'connector/documents/customer-b-doc/evidence-bundle' . $customerQuery, false);
    assertRequest(fn() => $customerA->advanced->documents->supportPacket($customerBDocument), 'GET', 'connector/documents/customer-b-doc/support-packet' . $customerQuery, false);

    assertRequest(
        fn() => $connector->customers->for('erp-customer-1')->advanced->mapper([
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
    assertTrue(($body['execute'] ?? null) === 'preview', 'Expected customer Mapper to be preview-only.');
    try {
        $connector->customers->for('erp-customer-1')->advanced->mapper(['execute' => 'send']);
        fail('Expected executable customer Mapper request to be rejected.');
    } catch (\InvalidArgumentException $error) {
        assertTrue(str_contains($error->getMessage(), 'only supports preview normalization'), 'Expected preview-only Mapper validation.');
    }

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
