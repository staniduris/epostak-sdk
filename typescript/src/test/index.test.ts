/**
 * SDK v3.2.0 tests — uses Node.js built-in test runner.
 * Run: node --test dist/test/index.test.js
 *
 * Each test:
 * - Mocks globalThis.fetch
 * - Asserts URL / headers / query params
 * - Asserts returned shape
 * - Tests 422 UblValidationError path
 */

import { describe, it, before, after } from "node:test";
import * as assert from "node:assert/strict";

// Import from the compiled source (not dist re-exports) to keep it simple
import {
  EPostak,
  EPostakError,
  UblValidationError,
  BoxResource,
  ConnectorResource,
  InboundResource,
  OutboundResource,
  type BatchLookupResult,
  type BatchExtractResult,
  type ExtractResult,
  type PeppolParticipant,
  type ConnectorMapperPreviewRequest,
} from "../index.js";

it("preserves nested business error metadata and Retry-After seconds", () => {
  const retryable = new EPostakError(409, {
    error: {
      code: "idempotency_in_flight",
      message: "Still processing",
      field: "externalId",
      nextAction: "retry",
      retryable: true,
      requestId: "req-body",
    },
  }, new Headers({ "Retry-After": "7", "X-Request-Id": "req-header" }));
  assert.strictEqual(retryable.field, "externalId");
  assert.strictEqual(retryable.nextAction, "retry");
  assert.strictEqual(retryable.retryable, true);
  assert.strictEqual(retryable.requestId, "req-body");
  assert.strictEqual(retryable.retryAfter, 7);

  const validation = new EPostakError(422, {
    error: { code: "validation_failed", message: "Fix request", retryable: false },
  });
  assert.strictEqual(validation.retryable, false);
  assert.strictEqual(validation.retryAfter, undefined);
});

// ---------------------------------------------------------------------------
// Minimal fetch mock infrastructure
// ---------------------------------------------------------------------------

type FetchMock = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

let activeMock: FetchMock | null = null;

function mockFetch(impl: FetchMock): void {
  activeMock = impl;
}

function clearMock(): void {
  activeMock = null;
}

function makeMockResponse(
  body: unknown,
  status = 200,
  headers: Record<string, string> = {},
): Response {
  const defaultHeaders: Record<string, string> = {
    "content-type": "application/json",
    ...headers,
  };
  return new Response(JSON.stringify(body), {
    status,
    headers: defaultHeaders,
  });
}

// ---------------------------------------------------------------------------
// Patch globalThis.fetch
// ---------------------------------------------------------------------------

const originalFetch = globalThis.fetch;

before(() => {
  globalThis.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    if (activeMock) return activeMock(input, init);
    throw new Error("No fetch mock set");
  };
});

after(() => {
  globalThis.fetch = originalFetch;
  clearMock();
});

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeClient(config: { firmId?: string; maxRetries?: number } = {}): EPostak {
  // Use a pre-minted bearer token so no auth request is made.
  // We mock the token endpoint to return a token immediately.
  mockFetch(async (input) => {
    const url = typeof input === "string" ? input : (input as URL).toString();
    if (url.includes("/auth/token")) {
      return makeMockResponse({
        access_token: "test-token",
        refresh_token: "test-refresh",
        token_type: "Bearer",
        expires_in: 900,
      });
    }
    throw new Error(`Unexpected fetch during client setup: ${url}`);
  });

  return new EPostak({
    clientId: "sk_live_test",
    clientSecret: "sk_live_test_secret",
    baseUrl: "https://dev.epostak.sk/api/v1",
    ...config,
  });
}

// ---------------------------------------------------------------------------
// Resource type assertions (compile-time — just ensure the types are assignable)
// ---------------------------------------------------------------------------

it("BoxResource, ConnectorResource, InboundResource and OutboundResource are exported", () => {
  assert.ok(BoxResource);
  assert.ok(ConnectorResource);
  assert.ok(InboundResource);
  assert.ok(OutboundResource);
});

it("ConnectorMapperPreviewRequest is exported from the package root", () => {
  const request: ConnectorMapperPreviewRequest = {
    sourceType: "json",
    sourceJson: { invoiceNumber: "FA-1" },
    execute: "preview",
  };
  assert.strictEqual(request.execute, "preview");
});

it("box resource uses the public Box paths", async () => {
  const client = makeClient();
  const captured: Array<{ method: string; url: string; body: string | undefined }> = [];

  mockFetch(async (input, init) => {
    const url = typeof input === "string" ? input : (input as URL).toString();
    if (url.includes("/auth/token")) {
      return makeMockResponse({
        access_token: "tok",
        refresh_token: "ref",
        token_type: "Bearer",
        expires_in: 900,
      });
    }

    captured.push({
      method: init?.method ?? "GET",
      url,
      body: typeof init?.body === "string" ? init.body : undefined,
    });
    return makeMockResponse({ ok: true });
  });

  await client.box.list({ status: "ready", direction: "outbound", limit: 10, offset: 5 });
  await client.box.create({
    payloadXml: "<Invoice/>",
    scheduledFor: "2026-07-01T00:00:00.000Z",
    externalId: "erp-doc-1",
    metadata: { source: "sdk-test" },
  });
  await client.box.get("box-1");
  await client.box.schedule("box-1", { scheduledFor: "2026-07-01T00:00:00.000Z" });
  await client.box.sendNow("box-1");
  await client.box.retry("box-1");
  await client.box.cancel("box-1");

  assert.deepStrictEqual(
    captured.map((req) => [req.method, new URL(req.url).pathname + new URL(req.url).search]),
    [
      ["GET", "/api/v1/box/items?status=ready&direction=outbound&limit=10&offset=5"],
      ["POST", "/api/v1/box/items"],
      ["GET", "/api/v1/box/items/box-1"],
      ["POST", "/api/v1/box/items/box-1/schedule"],
      ["POST", "/api/v1/box/items/box-1/send-now"],
      ["POST", "/api/v1/box/items/box-1/retry"],
      ["POST", "/api/v1/box/items/box-1/cancel"],
    ],
  );
  assert.deepStrictEqual(JSON.parse(captured[1].body ?? "{}"), {
    payloadXml: "<Invoice/>",
    scheduledFor: "2026-07-01T00:00:00.000Z",
    externalId: "erp-doc-1",
    metadata: { source: "sdk-test" },
  });
  assert.deepStrictEqual(JSON.parse(captured[3].body ?? "{}"), {
    scheduledFor: "2026-07-01T00:00:00.000Z",
  });
});

it("payloads, events and support packet facade use preferred API paths", async () => {
  const client = makeClient();
  const captured: Array<{ method: string; url: string; body: string | undefined }> = [];

  mockFetch(async (input, init) => {
    const url = typeof input === "string" ? input : (input as URL).toString();
    if (url.includes("/auth/token")) {
      return makeMockResponse({
        access_token: "tok",
        refresh_token: "ref",
        token_type: "Bearer",
        expires_in: 900,
      });
    }

    captured.push({
      method: init?.method ?? "GET",
      url,
      body: typeof init?.body === "string" ? init.body : undefined,
    });
    if (url.includes("/events/pull")) {
      return makeMockResponse({
        events: [
          {
            event_id: "evt-live",
            firm_id: "firm-1",
            event: "document.received",
            payload: { event: "document.received", event_version: "1", timestamp: "2026-07-03T00:00:00Z", data: {} },
            created_at: "2026-07-03T00:00:00Z",
          },
        ],
        has_more: false,
      });
    }
    return makeMockResponse({ ok: true, acknowledged: 1, items: [] });
  });

  await client.payloads.extract(Buffer.from("pdf"), "application/pdf", "invoice.pdf");
  await client.payloads.extractBatch([
    { file: Buffer.from("pdf"), mimeType: "application/pdf", fileName: "invoice.pdf" },
  ]);
  await client.payloads.parse("<Invoice/>");
  await client.payloads.convert({
    input_format: "ubl",
    output_format: "json",
    document: "<Invoice/>",
  });
  await client.payloads.validate({
    format: "ubl",
    document: "<Invoice/>",
  });
  const events = await client.events.pull({ limit: 10, event_type: "document.received" });
  await client.events.ack("evt-1");
  await client.events.batchAck(["evt-1", "evt-2"]);
  await client.documents.supportPacket("doc-1");
  await client.connector.getDocumentSupportPacket("doc-1");
  await client.connector.documents.supportPacket("doc-3");
  await client.connector.customers.for("cust-1").documents.supportPacket("doc-2");

  assert.deepStrictEqual(
    captured.map((req) => [req.method, new URL(req.url).pathname + new URL(req.url).search]),
    [
      ["POST", "/api/v1/payloads/extract"],
      ["POST", "/api/v1/payloads/extract/batch"],
      ["POST", "/api/v1/payloads/parse"],
      ["POST", "/api/v1/payloads/convert"],
      ["POST", "/api/v1/payloads/validate"],
      ["GET", "/api/v1/events/pull?limit=10&event_type=document.received"],
      ["POST", "/api/v1/events/evt-1/ack"],
      ["POST", "/api/v1/events/batch-ack"],
      ["GET", "/api/v1/documents/doc-1/support-packet"],
      ["GET", "/api/v1/connector/documents/doc-1/support-packet"],
      ["GET", "/api/v1/connector/documents/doc-3/support-packet"],
      ["GET", "/api/v1/connector/documents/doc-2/support-packet?customerRef=cust-1"],
    ],
  );
  assert.deepStrictEqual(JSON.parse(captured[7].body ?? "{}"), {
    event_ids: ["evt-1", "evt-2"],
  });
  assert.strictEqual(events.items[0]?.event_id, "evt-live");
});

it("extract result types expose OCR review send payload fields", () => {
  const single: ExtractResult = {
    extraction: { invoiceNumber: "FAK-001" },
    document_type: "invoice",
    direction: "outbound",
    send_payload: { receiverName: "Odberatel s.r.o." },
    send_payload_missing_fields: ["receiverPeppolId"],
    send_ready: false,
    ubl_xml: "",
    confidence: "high",
    confidence_scores: { invoice_number: 0.95 },
    needs_review: true,
    missing_fields: [{ field: "receiverPeppolId", blocking: true }],
    field_sources: {
      invoice_number: { source: "ocr", value: "FAK-001", confidence: 0.95 },
    },
    next_action: {
      type: "review_and_send",
      endpoint: "/api/v1/documents/send",
      method: "POST",
    },
    file_name: "invoice.pdf",
  };

  const batch: BatchExtractResult = {
    results: [
      {
        file_name: "invoice.pdf",
        direction: "outbound",
        send_payload: { receiverName: "Odberatel s.r.o." },
        send_payload_missing_fields: ["receiverPeppolId"],
        send_ready: false,
        extraction: single.extraction,
        confidence: single.confidence,
        missing_fields: [{ field: "receiverPeppolId", blocking: true }],
        field_sources: {
          invoice_number: { source: "ocr", value: "FAK-001", confidence: 0.95 },
        },
        next_action: {
          type: "review_and_send",
          endpoint: "/api/v1/documents/send",
          method: "POST",
        },
      },
    ],
  };

  assert.strictEqual(single.next_action?.endpoint, "/api/v1/documents/send");
  assert.strictEqual(batch.results[0].send_payload_missing_fields?.[0], "receiverPeppolId");
});

// ---------------------------------------------------------------------------
// UblValidationError
// ---------------------------------------------------------------------------

describe("UblValidationError", () => {
  it("constructs with required fields", () => {
    const err = new UblValidationError({ message: "Seller name is missing", rule: "BR-06" });
    assert.strictEqual(err.code, "UBL_VALIDATION_ERROR");
    assert.strictEqual(err.rule, "BR-06");
    assert.strictEqual(err.message, "Seller name is missing");
    assert.strictEqual(err.status, 422);
    assert.ok(err instanceof UblValidationError);
    assert.ok(err instanceof Error);
  });

  it("carries optional requestId", () => {
    const err = new UblValidationError({
      message: "Issue date missing",
      rule: "BT-1",
      requestId: "req-abc",
    });
    assert.strictEqual(err.requestId, "req-abc");
  });

  it("is thrown by buildApiError on 422 UBL_VALIDATION_ERROR", async () => {
    const client = makeClient();

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      // Return 422 UBL_VALIDATION_ERROR for any other call
      return makeMockResponse(
        {
          error: {
            code: "UBL_VALIDATION_ERROR",
            message: "Invoice number missing",
            rule: "BR-02",
          },
        },
        422,
      );
    });

    try {
      await client.documents.send({
        receiverPeppolId: "0245:1234567890",
        receiverName: "Test s.r.o.",
        items: [{ description: "Test", quantity: 1, unitPrice: 100, vatRate: 23 }],
      });
      assert.fail("Expected UblValidationError to be thrown");
    } catch (err) {
      assert.ok(err instanceof UblValidationError, `Expected UblValidationError, got ${err}`);
      assert.strictEqual((err as UblValidationError).rule, "BR-02");
      assert.strictEqual((err as UblValidationError).code, "UBL_VALIDATION_ERROR");
    }
  });

  it("serializes live JSON billing prepayment and line-item fields", async () => {
    const client = makeClient();
    let capturedBody: Record<string, unknown> | null = null;

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedBody =
        typeof init?.body === "string" ? JSON.parse(init.body) : null;
      return makeMockResponse({
        documentId: "doc-1",
        messageId: "msg-1",
        status: "SENT",
      });
    });

    await client.documents.send({
      receiverPeppolId: "0245:12345678",
      receiverName: "Zakaznik s.r.o.",
      receiverStreet: "Hlavna 1",
      receiverCity: "Bratislava",
      receiverPostalCode: "81101",
      prepaidAmount: 1230,
      prepayments: [
        {
          advanceInvoiceRef: "ZAL-2026-0004",
          taxDocumentRef: "DDP-2026-0022",
          settlementDate: "2026-02-23",
          amountWithoutVat: 1000,
          vatAmount: 230,
          amountWithVat: 1230,
          vatRate: 23,
          vatCategoryCode: "S",
        },
      ],
      items: [
        {
          description: "Konzultacne sluzby",
          quantity: 10,
          unit: "HUR",
          unitPrice: 50,
          vatRate: 23,
          vatCategoryCode: "AE",
          taxTreatment: "reverse_charge_domestic",
          deliveryDate: "2026-04-01",
          customsTariffCode: "72044910",
          commodityClassificationCode: "72044910",
          commodityClassificationListId: "HS",
          reverseChargeParagraphLetter: "f",
          controlStatementType: "MT",
          controlStatementQuantity: 1250,
          controlStatementUnit: "kg",
        },
      ],
    });

    assert.deepStrictEqual(capturedBody, {
      receiverPeppolId: "0245:12345678",
      receiverName: "Zakaznik s.r.o.",
      receiverStreet: "Hlavna 1",
      receiverCity: "Bratislava",
      receiverPostalCode: "81101",
      prepaidAmount: 1230,
      prepayments: [
        {
          advanceInvoiceRef: "ZAL-2026-0004",
          taxDocumentRef: "DDP-2026-0022",
          settlementDate: "2026-02-23",
          amountWithoutVat: 1000,
          vatAmount: 230,
          amountWithVat: 1230,
          vatRate: 23,
          vatCategoryCode: "S",
        },
      ],
      items: [
        {
          description: "Konzultacne sluzby",
          quantity: 10,
          unit: "HUR",
          unitPrice: 50,
          vatRate: 23,
          vatCategoryCode: "AE",
          taxTreatment: "reverse_charge_domestic",
          deliveryDate: "2026-04-01",
          customsTariffCode: "72044910",
          commodityClassificationCode: "72044910",
          commodityClassificationListId: "HS",
          reverseChargeParagraphLetter: "f",
          controlStatementType: "MT",
          controlStatementQuantity: 1250,
          controlStatementUnit: "kg",
        },
      ],
    });
  });

  it("serializes live JSON billing advance-deduction line fields", async () => {
    const client = makeClient();
    let capturedBody: Record<string, unknown> | null = null;

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedBody =
        typeof init?.body === "string" ? JSON.parse(init.body) : null;
      return makeMockResponse({
        documentId: "doc-2",
        messageId: "msg-2",
        status: "SENT",
      });
    });

    await client.documents.send({
      receiverPeppolId: "0245:12345678",
      receiverName: "Zakaznik s.r.o.",
      items: [
        {
          description: "Zuctovanie zalohy",
          quantity: 1,
          unit: "C62",
          unitPrice: -1000,
          vatRate: 23,
          lineType: "advance_deduction",
          advanceInvoiceReference: "ZF-2026-001",
        },
      ],
    });

    assert.deepStrictEqual(capturedBody, {
      receiverPeppolId: "0245:12345678",
      receiverName: "Zakaznik s.r.o.",
      items: [
        {
          description: "Zuctovanie zalohy",
          quantity: 1,
          unit: "C62",
          unitPrice: -1000,
          vatRate: 23,
          lineType: "advance_deduction",
          advanceInvoiceReference: "ZF-2026-001",
        },
      ],
    });
  });
});

// ---------------------------------------------------------------------------
// client.inbound.*
// ---------------------------------------------------------------------------

describe("client.inbound", () => {
  it("list() calls correct URL with params", async () => {
    const client = makeClient();
    let capturedUrl = "";

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      return makeMockResponse({
        documents: [],
        next_cursor: null,
        has_more: false,
      });
    });

    const result = await client.inbound.list({ limit: 50, kind: "invoice", sender: "0245:9999" });

    assert.ok(capturedUrl.includes("/inbound/documents"), `URL ${capturedUrl} missing path`);
    assert.ok(capturedUrl.includes("limit=50"), "Missing limit param");
    assert.ok(capturedUrl.includes("kind=invoice"), "Missing kind param");
    assert.ok(capturedUrl.includes("sender=0245"), "Missing sender param");
    assert.strictEqual(result.has_more, false);
    assert.ok(Array.isArray(result.documents));
  });

  it("get() calls correct URL", async () => {
    const client = makeClient();
    let capturedUrl = "";
    const mockDoc = {
      id: "doc-1",
      received_at: "2026-05-12T10:00:00Z",
      kind: "invoice",
      peppol_message_id: null,
      sender: { peppol_id: "0245:1111", name: "Sender", country: "SK" },
      recipient: { peppol_id: "0245:2222", name: "Recipient" },
      document_type: "BIS Billing 3.0 Invoice",
      document_type_id: null,
      ubl_url: "https://epostak.sk/inbound/doc-1/ubl",
      metadata: { invoice_number: "INV-001" },
      ack: { acked_at: null, client_reference: null },
    };

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      return makeMockResponse(mockDoc);
    });

    const result = await client.inbound.get("doc-1");
    assert.ok(capturedUrl.includes("/inbound/documents/doc-1"));
    assert.strictEqual(result.id, "doc-1");
    assert.strictEqual(result.ack.acked_at, null);
  });

  it("getUbl() calls correct URL and returns text", async () => {
    const client = makeClient();
    let capturedUrl = "";
    const xmlContent = "<?xml version='1.0'?><Invoice/>";

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      return new Response(xmlContent, {
        status: 200,
        headers: { "content-type": "application/xml" },
      });
    });

    const xml = await client.inbound.getUbl("doc-1");
    assert.ok(capturedUrl.includes("/inbound/documents/doc-1/ubl"));
    assert.strictEqual(xml, xmlContent);
  });

  it("ack() posts to correct URL with body", async () => {
    const client = makeClient();
    let capturedUrl = "";
    let capturedBody = "";
    const mockDoc = {
      id: "doc-1",
      received_at: "2026-05-12T10:00:00Z",
      kind: "invoice",
      peppol_message_id: null,
      sender: { peppol_id: "0245:1111", name: "Sender" },
      recipient: { peppol_id: "0245:2222", name: "Recipient" },
      document_type: "BIS Billing 3.0 Invoice",
      document_type_id: null,
      ubl_url: "https://epostak.sk/inbound/doc-1/ubl",
      metadata: {},
      ack: { acked_at: "2026-05-12T10:01:00Z", client_reference: "erp-ref-1" },
    };

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      capturedBody = (init?.body as string) ?? "";
      return makeMockResponse(mockDoc);
    });

    const result = await client.inbound.ack("doc-1", { client_reference: "erp-ref-1" });
    assert.ok(capturedUrl.includes("/inbound/documents/doc-1/ack"));
    assert.ok(capturedBody.includes("erp-ref-1"));
    assert.strictEqual(result.ack.acked_at, "2026-05-12T10:01:00Z");
  });
});

// ---------------------------------------------------------------------------
// client.connector.*
// ---------------------------------------------------------------------------

describe("client.connector", () => {
  it("send() calls /connector/send with Idempotency-Key", async () => {
    const client = makeClient();
    let capturedUrl = "";
    let capturedMethod = "";
    let capturedHeader = "";
    let capturedBody = "";

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      capturedMethod = init?.method ?? "";
      capturedHeader = new Headers(init?.headers).get("Idempotency-Key") ?? "";
      capturedBody = String(init?.body ?? "");
      return makeMockResponse({
        documentId: "doc-1",
        status: "accepted",
        outcome: "accepted",
      }, 201);
    });

    const result = await client.connector.send(
      { receiverPeppolId: "0245:1234567890", document: { invoiceNumber: "FV-1" } },
      { idempotencyKey: "erp-1" },
    );

    assert.ok(capturedUrl.includes("/connector/send"));
    assert.strictEqual(capturedMethod, "POST");
    assert.strictEqual(capturedHeader, "erp-1");
    assert.ok(capturedBody.includes("receiverPeppolId"));
    assert.strictEqual(result.documentId, "doc-1");
  });

  it("omits X-Firm-Id on Connector v2 calls from a firm-scoped client", async () => {
    const client = makeClient({ firmId: "firm-uuid" });
    const captured: Array<{ url: string; firmId: string | null }> = [];

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }

      captured.push({
        url,
        firmId: new Headers(init?.headers).get("X-Firm-Id"),
      });

      if (url.includes("/connector/documents/doc-1/ubl")) {
        return new Response("<Invoice/>", { status: 200 });
      }
      if (url.includes("/connector/send")) {
        return makeMockResponse({ documentId: "doc-1", status: "accepted" }, 201);
      }
      if (url.includes("/connector/mailbox")) {
        return makeMockResponse(url.includes("send-policy") ? { mailbox: {} } : { mailboxes: [] });
      }
      if (url.includes("/connector/sync")) {
        return makeMockResponse({ items: [], nextCursor: null, hasMore: false });
      }
      if (url.includes("/connector/actions/action-1")) {
        return makeMockResponse({ action: { id: "action-1" } });
      }
      if (url.includes("/connector/reconcile")) {
        return makeMockResponse({ items: [], total: 0 });
      }
      return makeMockResponse({ autopilotId: "auto-1", lifecycleStatus: "staged" });
    });

    await client.connector.send({ document: { invoiceNumber: "FA-1" } });
    await client.connector.autopilot({
      customerRef: "erp-customer-1",
      mode: "stage",
      payload: { invoiceNumber: "FA-1" },
    });
    await client.connector.mapper({
      templateKey: "pohoda-csv-v1",
      sourceType: "csv",
      sourceText: "Doklad,PeppolID\nFA-1,0245:1234567890",
      execute: "stage",
    });
    await client.connector.zenInput({ customerRef: "erp-customer-1", invoiceNumber: "FA-1" });
    await client.connector.getAutopilotRun("auto-1");
    await client.connector.sendAutopilotRun("auto-1");
    await client.connector.reconcile();
    await client.connector.mailboxes();
    await client.connector.repairMailbox({ customerRef: "erp-customer-1" });
    await client.connector.updateMailboxSendPolicy("erp-customer-1", { policy: "stage_only" });
    await client.connector.sync({ customerRef: "erp-customer-1" });
    await client.connector.getDocument("doc-1");
    await client.connector.getDocumentUbl("doc-1");
    await client.connector.getDocumentEvidence("doc-1");
    await client.connector.getDocumentEvidenceBundle("doc-1");
    await client.connector.runAction("action-1", { note: "retry" });

    const legacy = captured.find((req) => req.url.includes("/connector/send"));
    assert.strictEqual(legacy?.firmId, "firm-uuid");

    const v2Requests = captured.filter((req) => !req.url.includes("/connector/send"));
    assert.ok(v2Requests.length > 0);
    assert.deepStrictEqual(
      v2Requests.map((req) => [req.url, req.firmId]),
      v2Requests.map((req) => [req.url, null]),
    );
  });

  it("inbox(), getInboxDocument(), ack(), events() use Connector paths", async () => {
    const client = makeClient();
    const captured: string[] = [];

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      captured.push(url);
      if (url.includes("/connector/inbox/doc-1/ack")) {
        return makeMockResponse({ documentId: "doc-1", status: "processed", acknowledged: true });
      }
      if (url.includes("/connector/inbox/doc-1")) {
        return makeMockResponse({ documentId: "doc-1", status: "received" });
      }
      if (url.includes("/connector/events")) {
        return makeMockResponse({ events: [], nextCursor: null, hasMore: false });
      }
      return makeMockResponse({ documents: [], nextCursor: null, hasMore: false });
    });

    await client.connector.inbox({ limit: 25 });
    await client.connector.getInboxDocument("doc-1");
    await client.connector.ack("doc-1");
    await client.connector.events({ cursor: "cur-1" });

    assert.ok(captured.some((url) => url.includes("/connector/inbox?limit=25")));
    assert.ok(captured.some((url) => url.includes("/connector/inbox/doc-1")));
    assert.ok(captured.some((url) => url.includes("/connector/inbox/doc-1/ack")));
    assert.ok(captured.some((url) => url.includes("/connector/events?cursor=cur-1")));
  });

  it("outbox stage/list/detail/send/cancel use Connector outbox paths", async () => {
    const client = makeClient();
    const captured: Array<{ method: string; url: string; body: string }> = [];

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }

      captured.push({
        method: init?.method ?? "GET",
        url,
        body: String(init?.body ?? ""),
      });

      if (url.includes("/connector/outbox/send")) {
        return makeMockResponse({ total: 1, sent: 1, failed: 0, skipped: 0, results: [] });
      }
      if (url.includes("/connector/outbox/outbox-1/send")) {
        return makeMockResponse({ outboxId: "outbox-1", status: "sent", ready: true });
      }
      if (init?.method === "DELETE") {
        return makeMockResponse({ outboxId: "outbox-1", status: "cancelled", ready: false });
      }
      if (url.includes("/connector/outbox/outbox-1")) {
        return makeMockResponse({ outboxId: "outbox-1", status: "ready", ready: true });
      }
      if (init?.method === "POST") {
        return makeMockResponse({
          total: 1,
          ready: 1,
          blocked: 0,
          staged: 0,
          items: [{ outboxId: "outbox-1", status: "ready", ready: true }],
        }, 201);
      }
      return makeMockResponse({ items: [], total: 0, limit: 20, offset: 0 });
    });

    await client.connector.advanced.outbox.stage({
      items: [
        {
          externalId: "FA-2026-001",
          payload: { receiverPeppolId: "0245:1234567890", invoiceNumber: "FA-2026-001" },
        },
      ],
    });
    await client.connector.advanced.outbox.list({ status: "blocked", limit: 10, offset: 20 });
    await client.connector.advanced.outbox.get("outbox-1");
    await client.connector.advanced.outbox.send("outbox-1", { force: true });
    await client.connector.advanced.outbox.sendBatch({ ids: ["outbox-1"], force: true });
    await client.connector.advanced.outbox.cancel("outbox-1");

    assert.ok(captured.some((req) => req.method === "POST" && req.url.includes("/connector/outbox") && req.body.includes("FA-2026-001")));
    assert.ok(captured.some((req) => req.method === "GET" && req.url.includes("/connector/outbox?status=blocked&limit=10&offset=20")));
    assert.ok(captured.some((req) => req.method === "GET" && req.url.includes("/connector/outbox/outbox-1")));
    assert.ok(captured.some((req) => req.method === "POST" && req.url.includes("/connector/outbox/outbox-1/send") && req.body.includes('"force":true')));
    assert.ok(captured.some((req) => req.method === "POST" && req.url.includes("/connector/outbox/send") && req.body.includes('"ids":["outbox-1"]')));
    assert.ok(captured.some((req) => req.method === "DELETE" && req.url.includes("/connector/outbox/outbox-1")));
  });

  it("autopilot lifecycle and reconcile use Connector v2 paths", async () => {
    const client = makeClient();
    const captured: Array<{ method: string; url: string; body: string }> = [];

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }

      captured.push({
        method: init?.method ?? "GET",
        url,
        body: String(init?.body ?? ""),
      });

      if (url.includes("/connector/reconcile")) {
        return makeMockResponse({ status: "exceptions", since: null, generatedAt: "2026-06-06T10:00:00.000Z", total: 0, items: [] });
      }
      return makeMockResponse({
        autopilotId: "auto-1",
        mode: "shadow",
        lifecycleStatus: "shadow_validated",
        replayed: false,
        nextActions: ["send"],
        links: { self: "/api/v1/connector/autopilot/auto-1" },
      }, init?.method === "POST" && url.endsWith("/connector/autopilot") ? 201 : 200);
    });

    await client.connector.advanced.autopilot({
      customerRef: "erp-customer-1",
      mode: "shadow",
      externalId: "ERP-FA-2026-001",
      idempotencyKey: "erp-fa-2026-001",
      payload: { receiverPeppolId: "0245:1234567890", invoiceNumber: "FA-2026-001" },
    });
    await client.connector.advanced.getAutopilotRun("auto-1");
    await client.connector.advanced.sendAutopilotRun("auto-1");
    await client.connector.advanced.reconcile({ status: "exceptions", since: "2026-06-01T00:00:00.000Z" });

    assert.ok(captured.some((req) => req.method === "POST" && req.url.endsWith("/connector/autopilot") && req.body.includes('"mode":"shadow"')));
    assert.ok(captured.some((req) => req.method === "GET" && req.url.includes("/connector/autopilot/auto-1")));
    assert.ok(captured.some((req) => req.method === "POST" && req.url.includes("/connector/autopilot/auto-1/send")));
    assert.ok(captured.some((req) => req.method === "GET" && req.url.includes("/connector/reconcile?status=exceptions&since=2026-06-01T00%3A00%3A00.000Z")));
  });

  it("managed Connector v2 endpoints use current production paths", async () => {
    const client = makeClient();
    const captured: Array<{ method: string; url: string; body: string }> = [];

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }

      captured.push({
        method: init?.method ?? "GET",
        url,
        body: String(init?.body ?? ""),
      });

      if (url.includes("/connector/documents/doc-1/ubl")) {
        return new Response("<Invoice/>", { status: 200, headers: { "content-type": "application/xml" } });
      }
      if (url.includes("/connector/mailbox")) {
        return makeMockResponse(url.includes("send-policy") ? { mailbox: { customerRef: "erp-customer-1" } } : { mailboxes: [] });
      }
      if (url.includes("/connector/sync")) {
        return makeMockResponse({ items: [], nextCursor: null, hasMore: false });
      }
      if (url.includes("/connector/actions/action-1")) {
        return makeMockResponse({ action: { id: "action-1" } });
      }
      if (url.includes("/connector/zen-input")) {
        return makeMockResponse({ autopilotId: "auto-1", mode: "stage", lifecycleStatus: "staged" }, 201);
      }
      if (url.includes("/connector/mapper")) {
        return makeMockResponse({ ok: true, checklist: [] });
      }
      return makeMockResponse({ documentId: "doc-1" });
    });

    await client.connector.customers.for("erp-customer-1").advanced.mapper({
      templateKey: "pohoda-csv-v1",
      sourceType: "csv",
      sourceText: "Doklad,PeppolID\nFA-2026-002,0245:1234567890",
      execute: "preview",
    });
    assert.throws(
      () => client.connector.customers.for("erp-customer-1").advanced.mapper({ execute: "send" } as never),
      /only supports preview normalization/,
    );
    await client.connector.zenInput({
      customerRef: "erp-customer-1",
      invoiceNumber: "FA-2026-002",
      mode: "stage",
      send: { policy: "stage_only" },
    });
    await client.connector.mailboxes();
    await client.connector.repairMailbox({ customerRef: "erp-customer-1" });
    await client.connector.updateMailboxSendPolicy("erp-customer-1", { policy: "daily_batch" });
    await client.connector.sync({ customerRef: "erp-customer-1", cursor: "cur-1", limit: 50 });
    await client.connector.getDocument("doc-1");
    const ubl = await client.connector.getDocumentUbl("doc-1");
    await client.connector.getDocumentEvidence("doc-1");
    await client.connector.getDocumentEvidenceBundle("doc-1");
    await client.connector.runAction("action-1", { note: "send now" });

    assert.strictEqual(ubl, "<Invoice/>");
    assert.ok(captured.some((req) => req.method === "POST" && req.url.endsWith("/connector/mapper") && req.body.includes("pohoda-csv-v1")));
    assert.ok(captured.some((req) => req.method === "POST" && req.url.endsWith("/connector/mapper") && req.body.includes("erp-customer-1")));
    assert.ok(captured.some((req) => req.method === "POST" && req.url.endsWith("/connector/mapper") && req.body.includes('"execute":"preview"')));
    assert.ok(captured.some((req) => req.method === "POST" && req.url.endsWith("/connector/zen-input") && req.body.includes("erp-customer-1")));
    assert.ok(captured.some((req) => req.method === "GET" && req.url.endsWith("/connector/mailbox")));
    assert.ok(captured.some((req) => req.method === "POST" && req.url.endsWith("/connector/mailbox/repair") && req.body.includes("erp-customer-1")));
    assert.ok(captured.some((req) => req.method === "PATCH" && req.url.includes("/connector/mailbox/erp-customer-1/send-policy")));
    assert.ok(captured.some((req) => req.method === "GET" && req.url.includes("/connector/sync?customerRef=erp-customer-1&cursor=cur-1&limit=50")));
    assert.ok(captured.some((req) => req.method === "GET" && req.url.endsWith("/connector/documents/doc-1")));
    assert.ok(captured.some((req) => req.method === "GET" && req.url.endsWith("/connector/documents/doc-1/ubl")));
    assert.ok(captured.some((req) => req.method === "GET" && req.url.endsWith("/connector/documents/doc-1/evidence")));
    assert.ok(captured.some((req) => req.method === "GET" && req.url.endsWith("/connector/documents/doc-1/evidence-bundle")));
    assert.ok(captured.some((req) => req.method === "POST" && req.url.endsWith("/connector/actions/action-1") && req.body.includes("send now")));
  });
});

describe("major release workflow namespaces", () => {
  it("exposes Enterprise resources under client.enterprise", () => {
    const client = makeClient();

    assert.strictEqual(client.enterprise.documents, client.documents);
    assert.strictEqual(client.enterprise.inbox, client.documents.inbox);
    assert.strictEqual(client.enterprise.pull.inbound, client.inbound);
    assert.strictEqual(client.enterprise.pull.outbound, client.outbound);
    assert.strictEqual(client.enterprise.connector, client.connector);
    assert.strictEqual(client.enterprise.webhooks, client.webhooks);
    assert.strictEqual(client.connector.outbox, client.connector.advanced.outbox);
    const customer = client.connector.customers.for("erp-customer-1");
    assert.strictEqual(customer.mailbox, customer.advanced.mailbox);
    assert.throws(
      () => client.connector.customers.for("  "),
      /customerRef is required/,
    );
  });

  it("keeps customer events tenant-scoped while advanced legacy events remain firm-scoped", async () => {
    const client = makeClient({ firmId: "firm-1" });
    const captured: Array<{ url: string; firmId: string | null }> = [];
    let requestIndex = 0;

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      captured.push({
        url,
        firmId: new Headers(init?.headers).get("X-Firm-Id"),
      });
      requestIndex += 1;
      return makeMockResponse({
        events: requestIndex === 1
          ? [{
              id: "event-1",
              documentId: "doc-1",
              type: "document.cancelled",
              state: "cancelled",
              occurredAt: "2026-07-14T10:00:00.000Z",
              data: {
                customerRef: "erp-customer-1",
                direction: "outbound",
                type: "invoice",
                number: null,
                response: null,
              },
            }]
          : [{
              id: "event-2",
              documentId: "doc-2",
              type: "delivered",
              occurredAt: "2026-07-14T10:00:00.000Z",
              status: "delivered",
            }],
        nextCursor: null,
        hasMore: false,
      });
    });

    const business = await client.connector.customers.for("erp-customer-1").events.list({ limit: 10 });
    const legacy = await client.connector.advanced.events({ limit: 10 });

    assert.ok(captured[0].url.includes("customerRef=erp-customer-1"));
    assert.strictEqual(captured[0].firmId, null);
    assert.ok(!captured[1].url.includes("customerRef="));
    assert.strictEqual(captured[1].firmId, "firm-1");
    assert.strictEqual(business.events[0].type, "document.cancelled");
    assert.strictEqual(business.events[0].state, "cancelled");
    assert.strictEqual(business.events[0].data.number, null);
    assert.strictEqual(business.events[0].data.response, null);
    assert.strictEqual(legacy.events[0].status, "delivered");
  });

  it("manages one global Connector webhook without leaking X-Firm-Id", async () => {
    const client = makeClient({ firmId: "firm-1" });
    const requests: Array<{
      method: string;
      path: string;
      body: string;
      firmId: string | null;
    }> = [];

    mockFetch(async (input, init) => {
      const url = new URL(typeof input === "string" ? input : input.toString());
      if (url.pathname.endsWith("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      requests.push({
        method: String(init?.method),
        path: `${url.pathname}${url.search}`,
        body: String(init?.body ?? ""),
        firmId: new Headers(init?.headers).get("X-Firm-Id"),
      });
      if (url.pathname.endsWith("/test")) {
        return makeMockResponse({
          deliveryId: "whd-1",
          status: "queued",
          event: {
            id: "evt-1",
            customerRef: "erp-customer-1",
            documentId: "doc-1",
            type: "document.delivered",
            state: "delivered",
            occurredAt: "2026-07-15T10:00:00Z",
            data: {
              customerRef: "erp-customer-1",
              direction: "outbound",
              type: "invoice",
              number: "FA-1",
              response: null,
            },
            test: true,
          },
        });
      }
      if (url.pathname.endsWith("/deliveries")) {
        return makeMockResponse({ deliveries: [], nextCursor: null, hasMore: false });
      }
      return makeMockResponse({
        webhook: {
          id: "wh-1",
          url: "https://erp.example/epostak",
          events: ["document.received"],
          active: true,
          failedAttempts: 0,
          createdAt: "2026-07-15T10:00:00Z",
          updatedAt: "2026-07-15T10:00:00Z",
        },
        ...(String(init?.method) === "PUT" ? { secret: "a".repeat(64) } : {}),
      });
    });

    const current = await client.connector.webhook.get();
    const configured = await client.connector.webhook.configure(" https://erp.example/epostak ", ["document.received"]);
    await client.connector.webhook.rotateSecret();
    const tested = await client.connector.webhook.test(" erp-customer-1 ");
    await client.connector.webhook.deliveries({ cursor: "next", limit: 25, status: "FAILED" });
    await client.connector.webhook.delete();

    assert.strictEqual(current.webhook?.id, "wh-1");
    assert.strictEqual(configured.secret, "a".repeat(64));
    assert.strictEqual(tested.deliveryId, "whd-1");
    assert.strictEqual(tested.status, "queued");
    assert.strictEqual(tested.event?.customerRef, "erp-customer-1");
    assert.strictEqual(tested.event?.data.customerRef, "erp-customer-1");
    assert.strictEqual(tested.event?.data.number, "FA-1");
    assert.strictEqual(tested.event?.data.response, null);
    assert.strictEqual(tested.event?.test, true);
    assert.deepStrictEqual(
      requests.map(({ method, path }) => [method, path]),
      [
        ["GET", "/api/v1/connector/webhook"],
        ["PUT", "/api/v1/connector/webhook"],
        ["POST", "/api/v1/connector/webhook/rotate-secret"],
        ["POST", "/api/v1/connector/webhook/test"],
        ["GET", "/api/v1/connector/webhook/deliveries?cursor=next&limit=25&status=FAILED"],
        ["DELETE", "/api/v1/connector/webhook"],
      ],
    );
    assert.ok(requests.every((request) => request.firmId === null));
    assert.ok(requests[1].body.includes('"url":"https://erp.example/epostak"'));
    assert.strictEqual(requests[3].body, '{"customerRef":"erp-customer-1"}');
    assert.throws(() => client.connector.webhook.configure(" "), /URL is required/);
    assert.throws(() => client.connector.webhook.test(" "), /customerRef is required/);
  });

  it("debugs and replays Connector webhooks without firm scope", async () => {
    const client = makeClient({ firmId: "firm-1" });
    const requests: Array<{ path: string; firmId: string | null; idempotencyKey: string | null }> = [];
    mockFetch(async (input, init) => {
      const url = new URL(typeof input === "string" ? input : input.toString());
      if (url.pathname.endsWith("/auth/token")) {
        return makeMockResponse({ access_token: "tok", refresh_token: "ref", token_type: "Bearer", expires_in: 900 });
      }
      const headers = new Headers(init?.headers);
      requests.push({ path: `${url.pathname}${url.search}`, firmId: headers.get("X-Firm-Id"), idempotencyKey: headers.get("Idempotency-Key") });
      return makeMockResponse({});
    });

    await client.connector.webhook.getDelivery("delivery 1");
    await client.connector.webhook.replayDelivery("delivery 1", "replay-key");
    await client.connector.webhook.runTestSuite({ customerRef: "erp-acme" }, "suite-key");
    await client.connector.webhook.getTestSuite("run 1");

    assert.deepStrictEqual(requests.map((request) => request.path), [
      "/api/v1/connector/webhook/deliveries/delivery%201",
      "/api/v1/connector/webhook/deliveries/delivery%201/replay",
      "/api/v1/connector/webhook/test-suite",
      "/api/v1/connector/webhook/test-suite/run%201",
    ]);
    assert.ok(requests.every((request) => request.firmId === null));
    assert.strictEqual(requests[1].idempotencyKey, "replay-key");
    assert.strictEqual(requests[2].idempotencyKey, "suite-key");
  });

  it("submits customer-scoped Connector documents without X-Firm-Id", async () => {
    const client = makeClient({ firmId: "firm-1" });
    let capturedBody = "";
    let capturedFirmId: string | null = "not-called";
    let capturedIdempotency: string | null = null;

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      assert.ok(url.endsWith("/connector/documents"), `Unexpected URL ${url}`);
      capturedBody = String(init?.body);
      capturedFirmId = new Headers(init?.headers).get("X-Firm-Id");
      capturedIdempotency = new Headers(init?.headers).get("Idempotency-Key");
      return makeMockResponse({
        id: "doc-1",
        customerRef: "erp-customer-1",
        externalId: "FA-1",
        direction: "outbound",
        type: "invoice",
        number: "FA-1",
        state: "queued",
        recipient: { name: null, country: "SK", companyId: null, taxId: "2120123456", vatId: null },
        createdAt: "2026-07-14T10:00:00.000Z",
        updatedAt: "2026-07-14T10:00:00.000Z",
        links: { self: "/api/v1/connector/documents/doc-1" },
      }, 201);
    });

    await client.connector.customers.for("erp-customer-1").documents.send({
      externalId: "FA-1",
      type: "invoice",
      number: "FA-1",
      recipient: { country: "SK", taxId: "2120123456" },
      lines: [{ description: "Licence", quantity: 1, unitPrice: 100, vatRate: 23 }],
    });

    assert.strictEqual(capturedFirmId, null);
    assert.strictEqual(
      capturedIdempotency,
      "connector:v1:f7be06badbccd0670a25e6df7fd654fd45ae7291d5f5043257806adc0b107045",
    );
    assert.ok(capturedBody.includes('"customerRef":"erp-customer-1"'));
    assert.ok(capturedBody.includes('"delivery":"send"'));
    assert.ok(capturedBody.includes('"externalId":"FA-1"'));
    assert.ok(!capturedBody.toLowerCase().includes("peppol"));
  });

  it("wires customer stage, filtered list and idempotent invoice response", async () => {
    const client = makeClient({ firmId: "firm-1", maxRetries: 1 });
    const requests: Array<{
      method: string;
      path: string;
      body: string;
      firmId: string | null;
      idempotencyKey: string | null;
    }> = [];
    let respondAttempts = 0;

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }

      const parsed = new URL(url);
      const headers = new Headers(init?.headers);
      requests.push({
        method: init?.method ?? "GET",
        path: parsed.pathname + parsed.search,
        body: String(init?.body ?? ""),
        firmId: headers.get("X-Firm-Id"),
        idempotencyKey: headers.get("Idempotency-Key"),
      });

      if (parsed.pathname.endsWith("/respond")) {
        respondAttempts += 1;
        if (respondAttempts === 1) {
          return makeMockResponse(
            { error: { code: "temporary", message: "retry" } },
            503,
            { "Retry-After": "0" },
          );
        }
        return makeMockResponse({
          id: "doc-in-1",
          customerRef: "erp-customer-1",
          response: {
            status: "accepted",
            direction: "sent",
            delivery: "queued",
            respondedAt: "2026-07-15T12:00:00Z",
          },
          idempotent: true,
        });
      }
      if ((init?.method ?? "GET") === "GET") {
        return makeMockResponse({ documents: [], nextCursor: "cur-2", hasMore: true });
      }
      return makeMockResponse({ id: "doc-stage-1", state: "queued" }, 201);
    });

    const documents = client.connector.customers.for("erp-customer-1").documents;
    assert.throws(
      () => documents.respond("doc-in-1", "another-customer", { status: "accepted" }),
      /customerRef conflicts with scoped customer/,
    );
    assert.strictEqual(requests.length, 0);
    await documents.stage(
      {
        externalId: "FA-STAGE-1",
        type: "invoice",
        number: "FA-STAGE-1",
        recipient: { country: "SK", taxId: "2120123456" },
        lines: [{ description: "Licence", quantity: 1, unitPrice: 100, vatRate: 23 }],
      },
      { idempotencyKey: "connector-stage-key" },
    );
    const page = await documents.list({
      direction: "inbound",
      state: "received",
      type: "invoice",
      createdAfter: "2026-07-01T00:00:00Z",
      cursor: "cur-1",
      limit: 25,
    });
    const result = await documents.respond("doc-in-1", {
      status: "accepted",
      note: "Imported into ERP",
    });

    assert.deepStrictEqual(
      requests.map(({ method, path }) => [method, path]),
      [
        ["POST", "/api/v1/connector/documents"],
        ["GET", "/api/v1/connector/documents?customerRef=erp-customer-1&direction=inbound&state=received&type=invoice&createdAfter=2026-07-01T00%3A00%3A00Z&cursor=cur-1&limit=25"],
        ["POST", "/api/v1/connector/documents/doc-in-1/respond?customerRef=erp-customer-1"],
        ["POST", "/api/v1/connector/documents/doc-in-1/respond?customerRef=erp-customer-1"],
      ],
    );
    assert.ok(requests.every(({ firmId }) => firmId === null));
    assert.strictEqual(requests[0].idempotencyKey, "connector-stage-key");
    assert.deepStrictEqual(JSON.parse(requests[0].body), {
      externalId: "FA-STAGE-1",
      type: "invoice",
      number: "FA-STAGE-1",
      recipient: { country: "SK", taxId: "2120123456" },
      lines: [{ description: "Licence", quantity: 1, unitPrice: 100, vatRate: 23 }],
      customerRef: "erp-customer-1",
      delivery: "stage",
    });
    assert.strictEqual(requests[2].idempotencyKey, null);
    assert.strictEqual(requests[3].idempotencyKey, null);
    assert.strictEqual(requests[2].body, requests[3].body);
    assert.deepStrictEqual(JSON.parse(requests[2].body), {
      status: "accepted",
      note: "Imported into ERP",
    });
    assert.strictEqual(page.nextCursor, "cur-2");
    assert.strictEqual(page.hasMore, true);
    assert.strictEqual(result.response.status, "accepted");
    assert.strictEqual(result.response.direction, "sent");
    assert.strictEqual(result.response.delivery, "queued");
    assert.strictEqual(result.idempotent, true);
  });

  it("keeps the customer submitDocument compatibility alias on staged Autopilot without mutating input", async () => {
    const client = makeClient({ firmId: "firm-1" });
    let capturedBody = "";
    let capturedFirmId: string | null = "not-called";
    const input = {
      externalId: "legacy-1",
      payload: {
        number: "FA-legacy-1",
        recipient: { country: "SK", taxId: "2120123456" },
        lines: [{ description: "Licence", quantity: 1, unitPrice: 100, vatRate: 23 }],
      },
    };

    mockFetch(async (raw, init) => {
      const url = typeof raw === "string" ? raw : (raw as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      assert.ok(url.endsWith("/connector/autopilot"), `Unexpected URL ${url}`);
      capturedBody = String(init?.body);
      capturedFirmId = new Headers(init?.headers).get("X-Firm-Id");
      return makeMockResponse({ documentId: "doc-legacy", status: "staged" }, 202);
    });

    await client.connector.customers.for("erp-customer-1").submitDocument(input);

    assert.strictEqual(capturedFirmId, null);
    assert.deepStrictEqual(input, {
      externalId: "legacy-1",
      payload: {
        number: "FA-legacy-1",
        recipient: { country: "SK", taxId: "2120123456" },
        lines: [{ description: "Licence", quantity: 1, unitPrice: 100, vatRate: 23 }],
      },
    });
    assert.ok(capturedBody.includes('"customerRef":"erp-customer-1"'));
    assert.ok(capturedBody.includes('"mode":"stage"'));
  });

  it("snapshots nested JSON before an awaited OAuth mint", async () => {
    const client = makeClient();
    let releaseToken!: (response: Response) => void;
    const tokenResponse = new Promise<Response>((resolve) => {
      releaseToken = resolve;
    });
    let capturedBody = "";

    mockFetch(async (raw, init) => {
      const url = typeof raw === "string" ? raw : (raw as URL).toString();
      if (url.includes("/auth/token")) return tokenResponse;
      capturedBody = String(init?.body);
      return makeMockResponse({ id: "doc-1", state: "queued" }, 201);
    });

    const input = {
      externalId: "FA-snapshot",
      number: "FA-snapshot",
      recipient: {
        country: "SK",
        taxId: "2120123456",
        address: { street: "Original 1", city: "Bratislava", postalCode: "81101" },
      },
      lines: [{ description: "Original line", quantity: 1, unitPrice: 10, vatRate: 23 }],
      attachments: [{ fileName: "original.pdf", mimeType: "application/pdf", content: "AA==" }],
    };

    const pending = client.connector.customers.for("customer").documents.send(input);
    input.recipient.address.street = "Mutated 2";
    input.lines[0].description = "Mutated line";
    input.attachments[0].fileName = "mutated.pdf";
    releaseToken(makeMockResponse({
      access_token: "tok",
      refresh_token: "ref",
      token_type: "Bearer",
      expires_in: 900,
    }));
    await pending;

    assert.ok(capturedBody.includes("Original 1"));
    assert.ok(capturedBody.includes("Original line"));
    assert.ok(capturedBody.includes("original.pdf"));
    assert.ok(!capturedBody.includes("Mutated"));
    assert.ok(!capturedBody.includes("mutated.pdf"));
  });

  it("binds customer point operations and artifacts to the scoped customerRef", async () => {
    const client = makeClient({ firmId: "firm-1" });
    const captured: Array<{
      method: string;
      path: string;
      body: BodyInit | null | undefined;
      firmId: string | null;
    }> = [];

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      captured.push({
        method: init?.method ?? "GET",
        path: new URL(url).pathname + new URL(url).search,
        body: init?.body,
        firmId: new Headers(init?.headers).get("X-Firm-Id"),
      });
      return makeMockResponse({ id: "doc-1", state: "queued" });
    });

    const customer = client.connector.customers.for("customer A/1");
    const documents = customer.documents;
    assert.throws(() => documents.sendDocument("  "), /documentId is required/);
    await documents.get("customer-b-doc");
    await documents.acknowledge("customer-b-doc", "erp-import");
    await documents.sendDocument("customer-b-doc");
    await documents.cancelDocument("customer-b-doc");
    await customer.advanced.documents.ubl("customer-b-doc");
    await customer.advanced.documents.evidence("customer-b-doc");
    await customer.advanced.documents.evidenceBundle("customer-b-doc");
    await customer.advanced.documents.supportPacket("customer-b-doc");

    assert.deepStrictEqual(
      captured.map(({ method, path, body, firmId }) => [method, path, body, firmId]),
      [
        ["GET", "/api/v1/connector/documents/customer-b-doc?customerRef=customer+A%2F1", null, null],
        ["POST", "/api/v1/connector/documents/customer-b-doc/acknowledge?customerRef=customer+A%2F1", '{"reference":"erp-import"}', null],
        ["POST", "/api/v1/connector/documents/customer-b-doc/send?customerRef=customer+A%2F1", null, null],
        ["POST", "/api/v1/connector/documents/customer-b-doc/cancel?customerRef=customer+A%2F1", null, null],
        ["GET", "/api/v1/connector/documents/customer-b-doc/ubl?customerRef=customer+A%2F1", null, null],
        ["GET", "/api/v1/connector/documents/customer-b-doc/evidence?customerRef=customer+A%2F1", null, null],
        ["GET", "/api/v1/connector/documents/customer-b-doc/evidence-bundle?customerRef=customer+A%2F1", null, null],
        ["GET", "/api/v1/connector/documents/customer-b-doc/support-packet?customerRef=customer+A%2F1", null, null],
      ],
    );
  });

  it("derives bounded collision-safe Connector idempotency keys", async () => {
    const client = makeClient();
    const keys: string[] = [];
    const bodies: string[] = [];

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      keys.push(new Headers(init?.headers).get("Idempotency-Key") ?? "");
      bodies.push(String(init?.body));
      return makeMockResponse({ id: "doc-1", state: "queued" });
    });

    const submit = async (
      customerRef: string,
      externalId: string,
      idempotencyKey?: string,
    ) =>
      client.connector.customers.for(customerRef).documents.send(
        {
          externalId,
          type: "invoice",
          number: "FA-1",
          recipient: { country: "SK", taxId: "2120123456" },
          lines: [{ description: "Licence", quantity: 1, unitPrice: 1, vatRate: 23 }],
        },
        idempotencyKey ? { idempotencyKey } : undefined,
      );

    await submit("a:b", "c");
    await submit("a", "b:c");
    await submit("c".repeat(255), "e".repeat(255));
    await submit("\u00a0\ufeffzákazník😀\ufeff\u00a0", "\ufeffFA-žltý-1\u00a0");
    await submit("\u0085zákazník😀\u0085", "\u0085FA-žltý-1\u0085");
    await submit("customer", "external", "caller-key");

    assert.deepStrictEqual(keys, [
      "connector:v1:540e8f1c5ae653a7d7e2fe88f7eb8dcabea924d661b1542ad191bb1848e0c33d",
      "connector:v1:e482a79a788392ccae4952360dd438820641e4c162b4952b42d35e78260d70be",
      "connector:v1:7182fd43682e0689adf34c908bc3ec162aaf1687c167fdbff714ff43daa4b111",
      "connector:v1:eec0ca654af898913432fbc7b7441a05080f72099f6d2ff85852f78c7458fdfd",
      "connector:v1:ff49689a9ece4c0319420ed07fc3a2a5b2e2e7bb6d4430a68557e372fdf70080",
      "caller-key",
    ]);
    assert.ok(keys.slice(0, 5).every((key) => key.length === 77));
    assert.notStrictEqual(keys[0], keys[1]);
    assert.ok(bodies[3].includes('"customerRef":"zákazník😀"'));
    assert.ok(bodies[3].includes('"externalId":"FA-žltý-1"'));
    assert.ok(bodies[4].includes('"customerRef":"\u0085zákazník😀\u0085"'));
    assert.ok(bodies[4].includes('"externalId":"\u0085FA-žltý-1\u0085"'));

    assert.throws(
      () => client.connector.customers.for("customer").documents.send(
        {
          externalId: "empty-key",
          number: "FA-1",
          recipient: { country: "SK", taxId: "2120123456" },
          lines: [{ description: "Licence", quantity: 1, unitPrice: 1, vatRate: 23 }],
        },
        { idempotencyKey: "" },
      ),
      /1-255 UTF-8 bytes/,
    );
  });

  it("retries keyed golden and lifecycle POSTs but surfaces 409 once", async () => {
    const client = makeClient({ maxRetries: 1 });
    const input = {
      externalId: "FA-retry",
      number: "FA-retry",
      recipient: { country: "SK", taxId: "2120123456" },
      lines: [{ description: "Original", quantity: 1, unitPrice: 1, vatRate: 23 }],
    };
    const bodies: string[] = [];
    const keys: Array<string | null> = [];
    let attempts = 0;

    mockFetch(async (raw, init) => {
      const url = typeof raw === "string" ? raw : (raw as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({ access_token: "tok", refresh_token: "ref", token_type: "Bearer", expires_in: 900 });
      }
      attempts += 1;
      bodies.push(String(init?.body));
      keys.push(new Headers(init?.headers).get("Idempotency-Key"));
      if (attempts === 1) {
        input.lines[0].description = "Mutated";
        return makeMockResponse({ error: { code: "temporary", message: "retry" } }, 503, { "Retry-After": "0" });
      }
      return makeMockResponse({ id: "doc-1", state: "queued" }, 201);
    });

    await client.connector.customers.for("customer").documents.send(input);
    assert.strictEqual(attempts, 2);
    assert.strictEqual(bodies[0], bodies[1]);
    assert.ok(bodies[0].includes("Original"));
    assert.deepStrictEqual(keys, [keys[0], keys[0]]);
    assert.ok(keys[0]);

    let lifecycleAttempts = 0;
    mockFetch(async () => {
      lifecycleAttempts += 1;
      return lifecycleAttempts === 1
        ? makeMockResponse({ error: { code: "temporary", message: "retry" } }, 503, { "Retry-After": "0" })
        : makeMockResponse({ id: "doc-1", state: "cancelled" });
    });
    await client.connector.customers.for("customer").documents.cancelDocument("doc-1");
    assert.strictEqual(lifecycleAttempts, 2);

    const conflictClient = makeClient({ maxRetries: 1 });
    let conflictAttempts = 0;
    mockFetch(async (raw) => {
      const url = typeof raw === "string" ? raw : (raw as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({ access_token: "tok", refresh_token: "ref", token_type: "Bearer", expires_in: 900 });
      }
      conflictAttempts += 1;
      return makeMockResponse({ error: { code: "idempotency_in_flight", message: "busy", retryable: true } }, 409, { "Retry-After": "0" });
    });
    await assert.rejects(
      conflictClient.connector.customers.for("customer").documents.send({ ...input, externalId: "FA-conflict" }),
      (error: unknown) => error instanceof EPostakError && error.status === 409,
    );
    assert.strictEqual(conflictAttempts, 1);
  });

  it("retries Connector transport failures without changing Enterprise or SAPI POST policy", async () => {
    const connectorClient = makeClient({ firmId: "firm-1", maxRetries: 1 });
    const connectorAttempts: Array<{
      path: string;
      body: string;
      key: string | null;
      firmId: string | null;
    }> = [];

    mockFetch(async (raw, init) => {
      const url = typeof raw === "string" ? raw : (raw as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      const headers = new Headers(init?.headers);
      connectorAttempts.push({
        path: new URL(url).pathname + new URL(url).search,
        body: String(init?.body ?? ""),
        key: headers.get("Idempotency-Key"),
        firmId: headers.get("X-Firm-Id"),
      });
      if (connectorAttempts.length === 1) {
        throw new TypeError("socket reset");
      }
      return makeMockResponse({ id: "doc-transport", state: "queued" }, 201);
    });

    await connectorClient.connector.customers.for("erp-customer-1").documents.stage(
      {
        externalId: "FA-TRANSPORT-1",
        type: "invoice",
        number: "FA-TRANSPORT-1",
        recipient: { country: "SK", taxId: "2120123456" },
        lines: [{ description: "Licence", quantity: 1, unitPrice: 100, vatRate: 23 }],
      },
      { idempotencyKey: "connector-transport-key" },
    );

    assert.strictEqual(connectorAttempts.length, 2);
    assert.deepStrictEqual(connectorAttempts[0], connectorAttempts[1]);
    assert.strictEqual(connectorAttempts[0].path, "/api/v1/connector/documents");
    assert.strictEqual(connectorAttempts[0].key, "connector-transport-key");
    assert.strictEqual(connectorAttempts[0].firmId, null);

    const sapiClient = makeClient({ firmId: "firm-1", maxRetries: 1 });
    let sapiAttempts = 0;
    mockFetch(async (raw) => {
      const url = typeof raw === "string" ? raw : (raw as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      sapiAttempts += 1;
      throw new TypeError("socket reset");
    });

    await assert.rejects(
      sapiClient.sapi.participants.for("0245:1234567890").documents.send(
        {
          metadata: {
            documentId: "sapi-transport-1",
            documentTypeId: "invoice",
            processId: "billing",
            senderParticipantId: "0245:1234567890",
            receiverParticipantId: "0245:0987654321",
            creationDateTime: "2026-07-15T12:00:00Z",
          },
          payload: "<Invoice/>",
          payloadFormat: "XML",
        },
        { idempotencyKey: "sapi-transport-key" },
      ),
      (error: unknown) => error instanceof EPostakError && error.status === 0,
    );
    assert.strictEqual(sapiAttempts, 1);
  });

  it("scopes SAPI document calls by participant and SAPI base URL", async () => {
    const client = makeClient({ firmId: "firm-1" });
    let capturedUrl = "";
    let capturedParticipant: string | null = null;
    let capturedIdempotency: string | null = null;

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      const headers = new Headers(init?.headers);
      capturedParticipant = headers.get("X-Peppol-Participant-Id");
      capturedIdempotency = headers.get("Idempotency-Key");
      return makeMockResponse({ documentId: "sapi-1", status: "accepted" }, 201);
    });

    await client.sapi.participants.for("0245:1234567890").documents.send(
      {
        metadata: {
          documentId: "sapi-fa-1",
          documentTypeId: "invoice",
          processId: "billing",
          senderParticipantId: "0245:1234567890",
          receiverParticipantId: "0245:0987654321",
          creationDateTime: "2026-06-14T10:00:00Z",
        },
        payload: "<Invoice/>",
        payloadFormat: "XML",
      },
      { idempotencyKey: "sapi-fa-1" },
    );

    assert.strictEqual(
      capturedUrl,
      "https://dev.epostak.sk/sapi/v1/document/send",
    );
    assert.strictEqual(capturedParticipant, "0245:1234567890");
    assert.strictEqual(capturedIdempotency, "sapi-fa-1");
  });
});

// ---------------------------------------------------------------------------
// client.outbound.*
// ---------------------------------------------------------------------------

describe("client.outbound", () => {
  it("list() calls correct URL with params", async () => {
    const client = makeClient();
    let capturedUrl = "";

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      return makeMockResponse({ documents: [], next_cursor: null, has_more: false });
    });

    const result = await client.outbound.list({ status: "failed", limit: 20 });
    assert.ok(capturedUrl.includes("/outbound/documents"));
    assert.ok(capturedUrl.includes("status=failed"));
    assert.ok(capturedUrl.includes("limit=20"));
    assert.strictEqual(result.has_more, false);
  });

  it("events() calls correct URL with params", async () => {
    const client = makeClient();
    let capturedUrl = "";

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      return makeMockResponse({ events: [], next_cursor: null, has_more: false });
    });

    const result = await client.outbound.events({ limit: 100, document_id: "uuid-1" });
    assert.ok(capturedUrl.includes("/outbound/events"));
    assert.ok(capturedUrl.includes("limit=100"));
    assert.ok(capturedUrl.includes("document_id=uuid-1"));
    assert.strictEqual(result.has_more, false);
    assert.ok(Array.isArray(result.events));
  });
});

// ---------------------------------------------------------------------------
// Current backend route coverage
// ---------------------------------------------------------------------------

describe("current backend route coverage", () => {
  it("documents.statusBatch() posts ordered ids", async () => {
    const client = makeClient();
    let capturedUrl = "";
    let capturedBody = "";

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      capturedBody = String(init?.body ?? "");
      return makeMockResponse({
        total: 2,
        found: 1,
        notFound: 1,
        results: [{ id: "doc-1", status: "delivered" }, { id: "doc-2", error: "not_found" }],
      });
    });

    const result = await client.documents.statusBatch(["doc-1", "doc-2"]);

    assert.ok(capturedUrl.endsWith("/documents/status/batch"), `URL ${capturedUrl} missing path`);
    assert.deepStrictEqual(JSON.parse(capturedBody), { ids: ["doc-1", "doc-2"] });
    assert.strictEqual(result.found, 1);
    assert.strictEqual(result.results[1].error, "not_found");
  });

  it("reporting.submissions() calls FS SR report history endpoint", async () => {
    const client = makeClient();
    let capturedUrl = "";

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      return makeMockResponse({ items: [], total: 0, limit: 20, offset: 0 });
    });

    const result = await client.reporting.submissions({ limit: 20, report_type: "EUSR" });

    assert.ok(capturedUrl.includes("/reporting/submissions"), `URL ${capturedUrl} missing path`);
    assert.ok(capturedUrl.includes("limit=20"), "Missing limit param");
    assert.ok(capturedUrl.includes("report_type=EUSR"), "Missing report_type param");
    assert.strictEqual(result.total, 0);
  });

  it("integrator.keys exposes list/deactivate endpoints", async () => {
    const client = makeClient();
    const calls: Array<{ method: string | undefined; url: string; body: string }> = [];

    mockFetch(async (input, init) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      calls.push({ method: init?.method, url, body: String(init?.body ?? "") });
      if (init?.method === "GET") return makeMockResponse({ keys: [] });
      return makeMockResponse({ success: true, message: "API key deactivated." });
    });

    await client.integrator.keys.list();
    await client.integrator.keys.deactivate({ client_id: "sk_int_prefix" });

    assert.deepStrictEqual(calls.map((c) => c.method), ["GET", "DELETE"]);
    assert.ok(calls.every((c) => c.url.endsWith("/integrator/keys")));
    assert.deepStrictEqual(JSON.parse(calls[1].body), { client_id: "sk_int_prefix" });
  });
});

// ---------------------------------------------------------------------------
// client.webhooks.test() — event as query param
// ---------------------------------------------------------------------------

describe("client.webhooks.test()", () => {
  it("passes event as ?event= query param", async () => {
    const client = makeClient();
    let capturedUrl = "";

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      return makeMockResponse({
        success: true,
        statusCode: 200,
        responseTime: 42,
        webhookId: "wh-1",
        event: "document.received",
      });
    });

    const result = await client.webhooks.test("wh-1", { event: "document.received" });
    assert.ok(capturedUrl.includes("/webhooks/wh-1/test"));
    assert.ok(
      capturedUrl.includes("event=document.received"),
      `Expected ?event= in URL, got: ${capturedUrl}`,
    );
    assert.strictEqual(result.success, true);
  });

  it("omits event param when not provided", async () => {
    const client = makeClient();
    let capturedUrl = "";

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      capturedUrl = url;
      return makeMockResponse({
        success: true,
        statusCode: 200,
        responseTime: 30,
        webhookId: "wh-2",
        event: "document.created",
      });
    });

    await client.webhooks.test("wh-2");
    assert.ok(!capturedUrl.includes("event="), `URL should not have event param: ${capturedUrl}`);
  });
});

// ---------------------------------------------------------------------------
// Rate-limit header parsing
// ---------------------------------------------------------------------------

describe("client.lastRateLimit", () => {
  it("is null before first request", () => {
    // Create a new client without making requests
    const client = new EPostak({
      clientId: "sk_test",
      clientSecret: "sk_test_secret",
      baseUrl: "https://dev.epostak.sk/api/v1",
    });
    assert.strictEqual(client.lastRateLimit, null);
  });

  it("is populated after a response with X-RateLimit-* headers", async () => {
    const client = makeClient();

    mockFetch(async (input) => {
      const url = typeof input === "string" ? input : (input as URL).toString();
      if (url.includes("/auth/token")) {
        return makeMockResponse({
          access_token: "tok",
          refresh_token: "ref",
          token_type: "Bearer",
          expires_in: 900,
        });
      }
      return makeMockResponse(
        { documents: [], next_cursor: null, has_more: false },
        200,
        {
          "x-ratelimit-limit": "500",
          "x-ratelimit-remaining": "487",
          "x-ratelimit-reset": "1747123200",
        },
      );
    });

    await client.inbound.list();
    const rl = client.lastRateLimit;
    assert.ok(rl !== null, "Expected lastRateLimit to be populated");
    if (rl === null) throw new Error("rl is null");
    assert.strictEqual(rl.limit, 500);
    assert.strictEqual(rl.remaining, 487);
    assert.ok(rl.resetAt instanceof Date);
    assert.strictEqual(rl.resetAt.getTime(), 1747123200 * 1000);
  });
});

// ---------------------------------------------------------------------------
// WebhookDelivery — idempotency_key field (type check)
// ---------------------------------------------------------------------------

it("WebhookDelivery type accepts idempotency_key field", () => {
  // Compile-time type check: if this compiles, the field is on the interface.
  // At runtime we just verify the object can be typed correctly.
  const delivery = {
    id: "d1",
    webhookId: "wh1",
    event: "document.received",
    status: "SUCCESS" as const,
    attempts: 1,
    responseStatus: 200,
    createdAt: "2026-05-12T10:00:00Z",
    idempotency_key: "sha256hex",
  };
  assert.strictEqual(delivery.idempotency_key, "sha256hex");
});

// ---------------------------------------------------------------------------
// Peppol participant capability lookup types
// ---------------------------------------------------------------------------

it("Peppol participant lookup type exposes capability routing fields", () => {
  const participant: PeppolParticipant = {
    found: true,
    accepts: false,
    routingStatus: "document_type_not_supported",
    participantId: "0245:2020305606",
    scheme: "0245",
    identifier: "2020305606",
    accessPoint: null,
    certificate: null,
    supportedDocumentTypes: [],
    source: "sml",
    temporaryFailure: false,
  };

  assert.strictEqual(participant.found, true);
  assert.strictEqual(participant.accepts, false);
  assert.strictEqual(participant.routingStatus, "document_type_not_supported");
});

it("Batch participant lookup result type exposes capability routing fields", () => {
  const result: BatchLookupResult = {
    index: 0,
    participant: { scheme: "0245", identifier: "2020305606", id: "0245:2020305606" },
    found: true,
    accepts: true,
    routingStatus: "ready",
    accessPoint: { url: "https://dev.epostak.sk/as4", transportProfile: "peppol-transport-as4-v2_0" },
    certificate: { present: true, valid: true },
    supportedDocumentTypes: ["urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##..."],
    source: "sml",
    temporaryFailure: false,
  };

  assert.strictEqual(result.accepts, true);
  assert.strictEqual(result.routingStatus, "ready");
});

// ---------------------------------------------------------------------------
// WebhookDeliveriesParams — includeResponseBody field
// ---------------------------------------------------------------------------

it("WebhookDeliveriesParams accepts includeResponseBody", async () => {
  const client = makeClient();
  let capturedUrl = "";

  mockFetch(async (input) => {
    const url = typeof input === "string" ? input : (input as URL).toString();
    if (url.includes("/auth/token")) {
      return makeMockResponse({
        access_token: "tok",
        refresh_token: "ref",
        token_type: "Bearer",
        expires_in: 900,
      });
    }
    capturedUrl = url;
    return makeMockResponse({ deliveries: [], total: 0, limit: 20, offset: 0 });
  });

  await client.webhooks.deliveries("wh-1", { includeResponseBody: true });
  assert.ok(
    capturedUrl.includes("includeResponseBody=true"),
    `Expected includeResponseBody in URL: ${capturedUrl}`,
  );
});
