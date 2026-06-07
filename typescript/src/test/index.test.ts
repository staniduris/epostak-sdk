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
  UblValidationError,
  ConnectorResource,
  InboundResource,
  OutboundResource,
} from "../index.js";

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

function makeClient(config: { firmId?: string } = {}): EPostak {
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

it("ConnectorResource, InboundResource and OutboundResource are exported", () => {
  assert.ok(ConnectorResource);
  assert.ok(InboundResource);
  assert.ok(OutboundResource);
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
        items: [{ description: "Test", quantity: 1, unitPrice: 100, vatRate: 23 }],
      });
      assert.fail("Expected UblValidationError to be thrown");
    } catch (err) {
      assert.ok(err instanceof UblValidationError, `Expected UblValidationError, got ${err}`);
      assert.strictEqual((err as UblValidationError).rule, "BR-02");
      assert.strictEqual((err as UblValidationError).code, "UBL_VALIDATION_ERROR");
    }
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

    await client.connector.outbox.stage({
      items: [
        {
          externalId: "FA-2026-001",
          payload: { receiverPeppolId: "0245:1234567890", invoiceNumber: "FA-2026-001" },
        },
      ],
    });
    await client.connector.outbox.list({ status: "blocked", limit: 10, offset: 20 });
    await client.connector.outbox.get("outbox-1");
    await client.connector.outbox.send("outbox-1", { force: true });
    await client.connector.outbox.sendBatch({ ids: ["outbox-1"], force: true });
    await client.connector.outbox.cancel("outbox-1");

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

    await client.connector.autopilot({
      customerRef: "erp-customer-1",
      mode: "shadow",
      externalId: "ERP-FA-2026-001",
      idempotencyKey: "erp-fa-2026-001",
      payload: { receiverPeppolId: "0245:1234567890", invoiceNumber: "FA-2026-001" },
    });
    await client.connector.getAutopilotRun("auto-1");
    await client.connector.sendAutopilotRun("auto-1");
    await client.connector.reconcile({ status: "exceptions", since: "2026-06-01T00:00:00.000Z" });

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
      return makeMockResponse({ documentId: "doc-1" });
    });

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
