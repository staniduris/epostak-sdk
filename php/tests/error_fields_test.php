<?php

declare(strict_types=1);

namespace EPostak\Tests;

use EPostak\EPostakError;
use EPostak\UblValidationException;

require_once __DIR__ . '/../src/EPostakError.php';
require_once __DIR__ . '/../src/UblRule.php';
require_once __DIR__ . '/../src/UblValidationException.php';

function fail(string $message): void
{
    fwrite(STDERR, $message . PHP_EOL);
    exit(1);
}

function same(mixed $expected, mixed $actual, string $label): void
{
    if ($expected !== $actual) {
        fail("{$label}: expected " . var_export($expected, true) . ', got ' . var_export($actual, true));
    }
}

// Connector 422 body as the API returns it since 2026-10-27
// (shape: lib/connector/business-errors.ts in the ePostak repo).
$fields = [[
    'path' => 'items[0].unitPrice',
    'message' => 'unitPrice must be a number',
    'message_sk' => 'Jednotková cena musí byť číslo',
]];
$body = json_decode(json_encode([
    'error' => [
        'code' => 'DOCUMENT_INVALID',
        'message' => 'unitPrice must be a number',
        'retryable' => false,
        'nextAction' => 'Correct the indicated document field and retry.',
        'apiCode' => 'VALIDATION_ERROR',
        'message_sk' => 'Jednotková cena musí byť číslo',
        'fields' => $fields,
        'rule' => 'PEPPOL-EN16931-R003',
        'fix_hint' => 'Send unitPrice as a number.',
        'fix_hint_sk' => 'Pošlite unitPrice ako číslo.',
        'docs' => 'https://epostak.sk/api/docs',
        'requestId' => 'request-ubl-validation',
    ],
]), true);

$error = new EPostakError(422, $body, ['X-Request-Id' => ['header-id']]);
same(422, $error->getStatus(), 'status');
same('DOCUMENT_INVALID', $error->getErrorCode(), 'code');
same('unitPrice must be a number', $error->getMessage(), 'message');
same('Correct the indicated document field and retry.', $error->getNextAction(), 'nextAction');
same(false, $error->isRetryable(), 'retryable');
same('request-ubl-validation', $error->getRequestId(), 'requestId');
same(null, $error->getField(), 'field');
same('VALIDATION_ERROR', $error->getApiCode(), 'apiCode');
same('Jednotková cena musí byť číslo', $error->getMessageSk(), 'message_sk');
same($fields, $error->getFields(), 'fields');
same('PEPPOL-EN16931-R003', $error->getRule(), 'rule');
same('Send unitPrice as a number.', $error->getFixHint(), 'fix_hint');
same('Pošlite unitPrice ako číslo.', $error->getFixHintSk(), 'fix_hint_sk');
same('https://epostak.sk/api/docs', $error->getDocs(), 'docs');

// A body without the new fields, or with wrong types, gives null.
$plain = new EPostakError(409, ['error' => ['code' => 'IDEMPOTENCY_CONFLICT', 'message' => 'Conflict']]);
same('IDEMPOTENCY_CONFLICT', $plain->getErrorCode(), 'plain code');
foreach (['getApiCode', 'getMessageSk', 'getFields', 'getRule', 'getFixHint', 'getFixHintSk', 'getDocs'] as $getter) {
    same(null, $plain->{$getter}(), "plain {$getter}");
}
$wrong = new EPostakError(422, ['error' => [
    'code' => 'DOCUMENT_INVALID',
    'apiCode' => 42,
    'fields' => 'items[0]',
    'rule' => ['x'],
]]);
same(null, $wrong->getApiCode(), 'wrong apiCode');
same(null, $wrong->getFields(), 'wrong fields');
same(null, $wrong->getRule(), 'wrong rule');
$string = new EPostakError(0, ['error' => 'Network down']);
same('Network down', $string->getMessage(), 'string message');
same(null, $string->getApiCode(), 'string apiCode');

// The UBL subclass keeps its public $rule and also answers getRule().
$ubl = new UblValidationException(422, ['error' => [
    'code' => 'UBL_VALIDATION_ERROR',
    'message' => 'Buyer reference or order reference is required',
    'rule' => 'PEPPOL-EN16931-R003',
    'fix_hint_sk' => 'Doplňte referenciu kupujúceho.',
]]);
same('PEPPOL-EN16931-R003', $ubl->rule, 'ubl public rule');
same('PEPPOL-EN16931-R003', $ubl->getRule(), 'ubl getRule');
same('Doplňte referenciu kupujúceho.', $ubl->getFixHintSk(), 'ubl fix_hint_sk');

fwrite(STDOUT, "php error fields tests passed\n");
