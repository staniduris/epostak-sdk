import fs from "node:fs";
import path from "node:path";
import process from "node:process";

const root = path.resolve(import.meta.dirname, "..");
const manifestPath = path.join(root, "fixtures/sdk-surface-manifest.json");
const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
const failures = [];

function read(relativePath) {
  const absolutePath = path.join(root, relativePath);
  if (!fs.existsSync(absolutePath)) {
    failures.push(`${relativePath}: file is missing`);
    return "";
  }
  return fs.readFileSync(absolutePath, "utf8");
}

function readJson(relativePath) {
  const source = read(relativePath);
  if (!source) return {};
  try {
    return JSON.parse(source);
  } catch (error) {
    failures.push(`${relativePath}: invalid JSON (${error.message})`);
    return {};
  }
}

function requireText(relativePath, source, needle, label = "required surface") {
  if (!source.includes(needle)) {
    failures.push(`${relativePath}: missing ${label} ${JSON.stringify(needle)}`);
  }
}

function requireExactKeys(value, expectedKeys, label) {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    failures.push(`${label}: expected an object`);
    return;
  }
  const actual = Object.keys(value).sort();
  const expected = [...expectedKeys].sort();
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    failures.push(`${label}: expected exact keys ${expected.join(", ")}; got ${actual.join(", ")}`);
  }
}

function requireEnum(value, allowed, label) {
  if (!allowed.includes(value)) {
    failures.push(`${label}: expected one of ${allowed.join(", ")}; got ${JSON.stringify(value)}`);
  }
}

function requireNonBlankString(value, label) {
  if (typeof value !== "string" || value.trim().length === 0) {
    failures.push(`${label}: expected a non-blank string`);
  }
}

function sourceFiles(relativeRoot) {
  const absoluteRoot = path.join(root, relativeRoot);
  const files = [];
  const sourceExtensions = new Set([".ts", ".py", ".php", ".rb", ".java", ".cs"]);
  if (!fs.existsSync(absoluteRoot)) return files;

  for (const entry of fs.readdirSync(absoluteRoot, { withFileTypes: true })) {
    const relativePath = path.join(relativeRoot, entry.name);
    if (entry.isDirectory()) files.push(...sourceFiles(relativePath));
    else if (sourceExtensions.has(path.extname(entry.name))) files.push(relativePath);
  }
  return files;
}

function runCheck(check, prefix = "") {
  const source = read(check.file);
  const label = prefix ? `${prefix} ${check.name}` : check.name;
  for (const needle of check.contains) requireText(check.file, source, needle, label);
}

for (const relativePath of manifest.documentation) {
  const source = read(relativePath);
  for (const needle of manifest.documentationContains) {
    requireText(relativePath, source, needle, "documentation contract");
  }
}

for (const check of manifest.checks) runCheck(check);

for (const surface of manifest.stableSurfaces) {
  for (const check of surface.checks) runCheck(check, `${surface.sdk} ${surface.product}`);
}

for (const relativeRoot of manifest.sourceRoots) {
  for (const relativePath of sourceFiles(relativeRoot)) {
    const source = read(relativePath);
    for (const forbidden of manifest.forbiddenSourcePatterns) {
      if (source.includes(forbidden)) {
        failures.push(`${relativePath}: forbidden compatibility pattern ${JSON.stringify(forbidden)}`);
      }
    }
  }
}

const webhookContractPath = manifest.contracts.webhook.fixture;
const webhookContract = readJson(webhookContractPath);
requireExactKeys(
  webhookContract,
  ["version", "endpoint", "configurationResponse", "testRequest", "testResponse", "deliveriesResponse"],
  `${webhookContractPath} root`,
);
if (webhookContract.version !== 1) failures.push(`${webhookContractPath}: unsupported version`);
if (webhookContract.endpoint !== "/connector/webhook") {
  failures.push(`${webhookContractPath}: invalid global webhook endpoint`);
}

const configuration = webhookContract.configurationResponse ?? {};
requireExactKeys(configuration, ["webhook", "secret"], `${webhookContractPath} configurationResponse`);
requireExactKeys(
  configuration.webhook ?? {},
  ["id", "url", "events", "active", "failedAttempts", "createdAt", "updatedAt"],
  `${webhookContractPath} configurationResponse.webhook`,
);
requireNonBlankString(configuration.webhook?.id, `${webhookContractPath} webhook.id`);
requireNonBlankString(configuration.webhook?.url, `${webhookContractPath} webhook.url`);
if (!Array.isArray(configuration.webhook?.events) || configuration.webhook.events.length === 0) {
  failures.push(`${webhookContractPath}: webhook.events must be a non-empty array`);
}
if (typeof configuration.webhook?.active !== "boolean") {
  failures.push(`${webhookContractPath}: webhook.active must be boolean`);
}
if (!Number.isInteger(configuration.webhook?.failedAttempts) || configuration.webhook.failedAttempts < 0) {
  failures.push(`${webhookContractPath}: webhook.failedAttempts must be a non-negative integer`);
}
if (!/^[0-9a-f]{64}$/.test(configuration.secret ?? "")) {
  failures.push(`${webhookContractPath}: create-time secret must be a 64-character hex value`);
}

requireExactKeys(webhookContract.testRequest ?? {}, ["customerRef"], `${webhookContractPath} testRequest`);
requireNonBlankString(webhookContract.testRequest?.customerRef, `${webhookContractPath} testRequest.customerRef`);
const webhookTest = webhookContract.testResponse ?? {};
requireExactKeys(webhookTest, ["deliveryId", "status", "event"], `${webhookContractPath} testResponse`);
if (webhookTest.status !== "queued") failures.push(`${webhookContractPath}: test status must be queued`);
const webhookEvent = webhookTest.event ?? {};
requireExactKeys(
  webhookEvent,
  ["id", "type", "customerRef", "documentId", "state", "occurredAt", "data", "test"],
  `${webhookContractPath} testResponse.event`,
);
requireExactKeys(
  webhookEvent.data ?? {},
  ["customerRef", "direction", "type", "number", "response"],
  `${webhookContractPath} testResponse.event.data`,
);
if (webhookEvent.data?.response !== null) {
  failures.push(`${webhookContractPath}: test event data.response must be null`);
}
if (webhookEvent.test !== true) failures.push(`${webhookContractPath}: test event must expose test=true`);
if (
  webhookEvent.customerRef !== webhookContract.testRequest?.customerRef ||
  webhookEvent.data?.customerRef !== webhookEvent.customerRef
) {
  failures.push(`${webhookContractPath}: root and compatibility data customerRef values must match testRequest`);
}

const deliveryPage = webhookContract.deliveriesResponse ?? {};
requireExactKeys(deliveryPage, ["deliveries", "nextCursor", "hasMore"], `${webhookContractPath} deliveriesResponse`);
if (!Array.isArray(deliveryPage.deliveries) || deliveryPage.deliveries.length === 0) {
  failures.push(`${webhookContractPath}: deliveriesResponse.deliveries must contain an example`);
} else {
  for (const [index, delivery] of deliveryPage.deliveries.entries()) {
    requireExactKeys(
      delivery,
      [
        "id", "webhookId", "eventId", "customerRef", "type", "status", "attempts",
        "responseStatus", "responseTimeMs", "lastAttemptAt", "nextRetryAt", "createdAt",
      ],
      `${webhookContractPath} deliveriesResponse.deliveries[${index}]`,
    );
    requireEnum(delivery.status, ["PENDING", "SUCCESS", "FAILED", "RETRYING"], `${webhookContractPath} delivery.status`);
  }
}
if (typeof deliveryPage.hasMore !== "boolean") {
  failures.push(`${webhookContractPath}: deliveriesResponse.hasMore must be boolean`);
}

for (const relativePath of manifest.contracts.webhook.testFiles) {
  const source = read(relativePath);
  for (const needle of manifest.contracts.webhook.testContains) {
    requireText(relativePath, source, needle, "global webhook response-shape test");
  }
}

const invoiceContractPath = manifest.contracts.invoiceResponse.fixture;
const invoiceContract = readJson(invoiceContractPath);
requireExactKeys(
  invoiceContract,
  ["version", "endpointTemplate", "statuses", "request", "response", "documentProjection"],
  `${invoiceContractPath} root`,
);
if (invoiceContract.version !== 1) failures.push(`${invoiceContractPath}: unsupported version`);
if (invoiceContract.endpointTemplate !== "/connector/documents/{documentId}/respond?customerRef={customerRef}") {
  failures.push(`${invoiceContractPath}: invalid respond endpoint template`);
}
const invoiceStatuses = [
  "received",
  "in_process",
  "under_query",
  "conditionally_accepted",
  "rejected",
  "accepted",
  "paid",
];
if (JSON.stringify(invoiceContract.statuses) !== JSON.stringify(invoiceStatuses)) {
  failures.push(`${invoiceContractPath}: statuses must match the canonical business enum`);
}
requireExactKeys(invoiceContract.request ?? {}, ["status", "note"], `${invoiceContractPath} request`);
requireEnum(invoiceContract.request?.status, invoiceStatuses, `${invoiceContractPath} request.status`);
const invoiceResponse = invoiceContract.response ?? {};
requireExactKeys(invoiceResponse, ["id", "customerRef", "response", "idempotent"], `${invoiceContractPath} response`);
requireExactKeys(
  invoiceResponse.response ?? {},
  ["status", "direction", "delivery", "respondedAt"],
  `${invoiceContractPath} response.response`,
);
requireEnum(invoiceResponse.response?.status, invoiceStatuses, `${invoiceContractPath} response.response.status`);
if (invoiceResponse.response?.direction !== "sent") {
  failures.push(`${invoiceContractPath}: respond result direction must be sent`);
}
requireEnum(invoiceResponse.response?.delivery, ["sent", "queued"], `${invoiceContractPath} response.response.delivery`);
if (typeof invoiceResponse.idempotent !== "boolean") {
  failures.push(`${invoiceContractPath}: response.idempotent must be boolean`);
}
const projection = invoiceContract.documentProjection?.response ?? {};
requireExactKeys(
  invoiceContract.documentProjection ?? {},
  ["response"],
  `${invoiceContractPath} documentProjection`,
);
requireExactKeys(
  projection,
  ["status", "direction", "reason", "respondedAt"],
  `${invoiceContractPath} documentProjection.response`,
);
requireEnum(projection.status, invoiceStatuses, `${invoiceContractPath} documentProjection.response.status`);
requireEnum(projection.direction, ["sent", "received"], `${invoiceContractPath} documentProjection.response.direction`);

for (const check of manifest.contracts.invoiceResponse.sourceChecks) {
  runCheck(check, "Connector invoice response contract");
}

const vectors = readJson(manifest.idempotencyVectors);
if (vectors.endpoint !== "/connector/documents" || vectors.omitFirmId !== true) {
  failures.push(`${manifest.idempotencyVectors}: invalid Connector transport contract`);
}
for (const vector of vectors.vectors ?? []) {
  if (!/^connector:v1:[0-9a-f]{64}$/.test(vector.idempotencyKey)) {
    failures.push(`${manifest.idempotencyVectors}: invalid key for ${vector.name}`);
  }
  for (const relativePath of manifest.idempotencyVectorTestFiles) {
    requireText(relativePath, read(relativePath), vector.idempotencyKey, `idempotency vector ${vector.name}`);
  }
}

if (failures.length > 0) {
  console.error(`SDK surface manifest failed with ${failures.length} issue(s):`);
  for (const failure of failures) console.error(`- ${failure}`);
  process.exit(1);
}

const stableCheckCount = manifest.stableSurfaces.reduce((count, surface) => count + surface.checks.length, 0);
console.log(
  `SDK surface manifest passed: ${manifest.checks.length} product checks, ` +
  `${stableCheckCount} stable Enterprise/SAPI checks, ${manifest.documentation.length} docs, ` +
  `2 response contracts, ${vectors.vectors.length} shared vectors.`,
);
