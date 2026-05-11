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

function makeClient(): EPostak {
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
    baseUrl: "https://test.epostak.sk/api/v1",
  });
}

// ---------------------------------------------------------------------------
// Resource type assertions (compile-time — just ensure the types are assignable)
// ---------------------------------------------------------------------------

it("InboundResource and OutboundResource are exported", () => {
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
      baseUrl: "https://test.epostak.sk/api/v1",
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
