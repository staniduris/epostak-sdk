---
name: ePostak SDK Review
description: First architectural review of @epostak/sdk TypeScript package — API design, pagination, retry, types, packaging
type: project
---

Reviewed 2026-04-26. Zero deps, ESM-only, Node 18+.

**Strengths**: Resource hierarchy is clean (client.documents.inbox, client.webhooks.queue), retry with backoff+jitter+Retry-After, idempotency keys, withFirm() pattern, strong JSDoc, encodeURIComponent everywhere, exhaustive types.

**Gaps found**: No pagination iterator/async generator, no CJS exports field, no HMAC verification helper, camelCase/snake_case inconsistency across endpoints (InboxAllDocument vs InboxDocument), Buffer return on pdf()/envelope() not portable to non-Node runtimes, no AbortSignal support, extract.single JSDoc says confidence is 0.95 number but type is bucket string.

**Why:** retention is project context for future SDK reviews.
**How to apply:** check these gaps against any future SDK changes before approving.
