import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");

const checks = [
  ["typescript/src/client.ts", "sapi: SapiResource"],
  ["typescript/src/client.ts", "sapi = new SapiResource"],
  ["typescript/src/resources/sapi.ts", "/sapi/v1/document/send"],
  ["typescript/src/resources/sapi.ts", "/sapi/v1/document/receive"],
  ["typescript/src/resources/sapi.ts", "/acknowledge"],
  ["typescript/src/resources/peppol.ts", "/company/search"],
  ["typescript/src/resources/peppol.ts", "/peppol/participants/resolve"],
  ["typescript/src/resources/documents.ts", "/peppol-documents"],
  ["typescript/src/resources/account.ts", "/licenses/info"],
  ["typescript/src/resources/documents.ts", "evidenceBundle"],
  ["typescript/src/resources/documents.ts", "/evidence-bundle"],
  ["typescript/src/resources/documents.ts", "statusBatch"],
  ["typescript/src/resources/documents.ts", "/documents/status/batch"],
  ["typescript/src/resources/outbound.ts", "getMdn"],
  ["typescript/src/resources/outbound.ts", "/mdn"],
  ["typescript/src/resources/webhooks.ts", "mode"],
  ["typescript/src/resources/webhooks.ts", "count"],
  ["typescript/src/resources/webhooks.ts", "testRunId"],
  ["typescript/src/resources/webhooks.ts", "/webhook-dead-letter"],
  ["typescript/src/resources/reporting.ts", "submissions"],
  ["typescript/src/resources/reporting.ts", "/reporting/submissions"],
  ["typescript/src/resources/integrator.ts", "keys = new IntegratorKeysResource"],
  ["typescript/src/resources/integrator.ts", "/integrator/keys"],
  ["typescript/src/resources/integrator.ts", "this.request(\"DELETE\", \"/integrator/keys\""],
  ["typescript/src/client.ts", "connector: ConnectorResource"],
  ["typescript/src/client.ts", "connector = new ConnectorResource"],
  ["typescript/src/resources/connector.ts", "/connector/preflight"],
  ["typescript/src/resources/connector.ts", "/connector/send"],
  ["typescript/src/resources/connector.ts", "/connector/status/"],
  ["typescript/src/resources/connector.ts", "/connector/inbox"],
  ["typescript/src/resources/connector.ts", "/connector/inbox/${encodeURIComponent(documentId)}"],
  ["typescript/src/resources/connector.ts", "/connector/inbox/${encodeURIComponent(documentId)}/ack"],
  ["typescript/src/resources/connector.ts", "/connector/events"],

  ["python/src/epostak/client.py", "self.sapi = SapiResource"],
  ["python/src/epostak/resources/sapi.py", "/sapi/v1/document/send"],
  ["python/src/epostak/resources/sapi.py", "/sapi/v1/document/receive"],
  ["python/src/epostak/resources/sapi.py", "/acknowledge"],
  ["python/src/epostak/resources/peppol.py", "/company/search"],
  ["python/src/epostak/resources/peppol.py", "/peppol/participants/resolve"],
  ["python/src/epostak/resources/documents.py", "/peppol-documents"],
  ["python/src/epostak/resources/account.py", "/licenses/info"],
  ["python/src/epostak/resources/documents.py", "evidence_bundle"],
  ["python/src/epostak/resources/documents.py", "/evidence-bundle"],
  ["python/src/epostak/resources/documents.py", "status_batch"],
  ["python/src/epostak/resources/documents.py", "/documents/status/batch"],
  ["python/src/epostak/resources/outbound.py", "get_mdn"],
  ["python/src/epostak/resources/outbound.py", "/mdn"],
  ["python/src/epostak/resources/webhooks.py", "mode"],
  ["python/src/epostak/resources/webhooks.py", "count"],
  ["python/src/epostak/resources/webhooks.py", "testRunId"],
  ["python/src/epostak/resources/webhooks.py", "/webhook-dead-letter"],
  ["python/src/epostak/resources/reporting.py", "submissions"],
  ["python/src/epostak/resources/reporting.py", "/reporting/submissions"],
  ["python/src/epostak/resources/integrator.py", "self.keys = IntegratorKeysResource"],
  ["python/src/epostak/resources/integrator.py", "/integrator/keys"],
  ["python/src/epostak/resources/integrator.py", "\"DELETE\", \"/integrator/keys\""],
  ["python/src/epostak/client.py", "self.connector = ConnectorResource"],
  ["python/src/epostak/resources/connector.py", "/connector/preflight"],
  ["python/src/epostak/resources/connector.py", "/connector/send"],
  ["python/src/epostak/resources/connector.py", "/connector/status/"],
  ["python/src/epostak/resources/connector.py", "/connector/inbox"],
  ["python/src/epostak/resources/connector.py", "/connector/inbox/{quote(document_id, safe='')}"],
  ["python/src/epostak/resources/connector.py", "/connector/inbox/{quote(document_id, safe='')}/ack"],
  ["python/src/epostak/resources/connector.py", "/connector/events"],

  ["ruby/lib/epostak/client.rb", "@sapi"],
  ["ruby/lib/epostak/resources/sapi.rb", "/sapi/v1/document/send"],
  ["ruby/lib/epostak/resources/sapi.rb", "/sapi/v1/document/receive"],
  ["ruby/lib/epostak/resources/sapi.rb", "/acknowledge"],
  ["ruby/lib/epostak/resources/peppol.rb", "/company/search"],
  ["ruby/lib/epostak/resources/peppol.rb", "/peppol/participants/resolve"],
  ["ruby/lib/epostak/resources/documents.rb", "/peppol-documents"],
  ["ruby/lib/epostak/resources/account.rb", "/licenses/info"],
  ["ruby/lib/epostak/resources/documents.rb", "evidence_bundle"],
  ["ruby/lib/epostak/resources/documents.rb", "/evidence-bundle"],
  ["ruby/lib/epostak/resources/documents.rb", "status_batch"],
  ["ruby/lib/epostak/resources/documents.rb", "/documents/status/batch"],
  ["ruby/lib/epostak/resources/outbound.rb", "get_mdn"],
  ["ruby/lib/epostak/resources/outbound.rb", "/mdn"],
  ["ruby/lib/epostak/resources/webhooks.rb", "mode"],
  ["ruby/lib/epostak/resources/webhooks.rb", "count"],
  ["ruby/lib/epostak/resources/webhooks.rb", "testRunId"],
  ["ruby/lib/epostak/resources/webhooks.rb", "/webhook-dead-letter"],
  ["ruby/lib/epostak/resources/reporting.rb", "submissions"],
  ["ruby/lib/epostak/resources/reporting.rb", "/reporting/submissions"],
  ["ruby/lib/epostak/resources/integrator.rb", "@keys"],
  ["ruby/lib/epostak/resources/integrator.rb", "/integrator/keys"],
  ["ruby/lib/epostak/resources/integrator.rb", ":delete, \"/integrator/keys\""],
  ["ruby/lib/epostak/client.rb", "@connector"],
  ["ruby/lib/epostak/resources/connector.rb", "/connector/preflight"],
  ["ruby/lib/epostak/resources/connector.rb", "/connector/send"],
  ["ruby/lib/epostak/resources/connector.rb", "/connector/status/"],
  ["ruby/lib/epostak/resources/connector.rb", "/connector/inbox"],
  ["ruby/lib/epostak/resources/connector.rb", "/connector/inbox/#{encode(document_id)}"],
  ["ruby/lib/epostak/resources/connector.rb", "/connector/inbox/#{encode(document_id)}/ack"],
  ["ruby/lib/epostak/resources/connector.rb", "/connector/events"],

  ["php/src/EPostak.php", "public Sapi $sapi"],
  ["php/src/Resources/Sapi.php", "/sapi/v1/document/send"],
  ["php/src/Resources/Sapi.php", "/sapi/v1/document/receive"],
  ["php/src/Resources/Sapi.php", "/acknowledge"],
  ["php/src/Resources/Peppol.php", "/company/search"],
  ["php/src/Resources/Peppol.php", "/peppol/participants/resolve"],
  ["php/src/Resources/Documents.php", "/peppol-documents"],
  ["php/src/Resources/Account.php", "/licenses/info"],
  ["php/src/Resources/Documents.php", "evidenceBundle"],
  ["php/src/Resources/Documents.php", "/evidence-bundle"],
  ["php/src/Resources/Documents.php", "statusBatch"],
  ["php/src/Resources/Documents.php", "/documents/status/batch"],
  ["php/src/Resources/Outbound.php", "getMdn"],
  ["php/src/Resources/Outbound.php", "/mdn"],
  ["php/src/Resources/Webhooks.php", "mode"],
  ["php/src/Resources/Webhooks.php", "count"],
  ["php/src/Resources/Webhooks.php", "testRunId"],
  ["php/src/Resources/Webhooks.php", "/webhook-dead-letter"],
  ["php/src/Resources/Reporting.php", "submissions"],
  ["php/src/Resources/Reporting.php", "/reporting/submissions"],
  ["php/src/Resources/Integrator.php", "public IntegratorKeys $keys"],
  ["php/src/Resources/Integrator.php", "/integrator/keys"],
  ["php/src/Resources/Integrator.php", "'DELETE', '/integrator/keys'"],
  ["php/src/EPostak.php", "public Connector $connector"],
  ["php/src/Resources/Connector.php", "/connector/preflight"],
  ["php/src/Resources/Connector.php", "/connector/send"],
  ["php/src/Resources/Connector.php", "/connector/status/"],
  ["php/src/Resources/Connector.php", "/connector/inbox"],
  ["php/src/Resources/Connector.php", "/connector/inbox/' . urlencode($documentId)"],
  ["php/src/Resources/Connector.php", "/connector/inbox/' . urlencode($documentId) . '/ack"],
  ["php/src/Resources/Connector.php", "/connector/events"],

  ["dotnet/src/EPostak/EPostakClient.cs", "public SapiResource Sapi"],
  ["dotnet/src/EPostak/Resources/SapiResource.cs", "/sapi/v1/document/send"],
  ["dotnet/src/EPostak/Resources/SapiResource.cs", "/sapi/v1/document/receive"],
  ["dotnet/src/EPostak/Resources/SapiResource.cs", "/acknowledge"],
  ["dotnet/src/EPostak/Resources/PeppolResource.cs", "/company/search"],
  ["dotnet/src/EPostak/Resources/PeppolResource.cs", "/peppol/participants/resolve"],
  ["dotnet/src/EPostak/Resources/DocumentsResource.cs", "/peppol-documents"],
  ["dotnet/src/EPostak/Resources/AccountResource.cs", "/licenses/info"],
  ["dotnet/src/EPostak/Resources/DocumentsResource.cs", "EvidenceBundleAsync"],
  ["dotnet/src/EPostak/Resources/DocumentsResource.cs", "/evidence-bundle"],
  ["dotnet/src/EPostak/Resources/DocumentsResource.cs", "StatusBatchAsync"],
  ["dotnet/src/EPostak/Resources/DocumentsResource.cs", "/documents/status/batch"],
  ["dotnet/src/EPostak/Resources/OutboundResource.cs", "GetMdnAsync"],
  ["dotnet/src/EPostak/Resources/OutboundResource.cs", "/mdn"],
  ["dotnet/src/EPostak/Resources/WebhooksResource.cs", "Mode"],
  ["dotnet/src/EPostak/Resources/WebhooksResource.cs", "Count"],
  ["dotnet/src/EPostak/Resources/WebhooksResource.cs", "TestRunId"],
  ["dotnet/src/EPostak/Resources/WebhooksResource.cs", "/webhook-dead-letter"],
  ["dotnet/src/EPostak/Resources/ReportingResource.cs", "SubmissionsAsync"],
  ["dotnet/src/EPostak/Resources/ReportingResource.cs", "/reporting/submissions"],
  ["dotnet/src/EPostak/Resources/IntegratorResource.cs", "IntegratorKeysResource Keys"],
  ["dotnet/src/EPostak/Resources/IntegratorResource.cs", "/integrator/keys"],
  ["dotnet/src/EPostak/Resources/IntegratorResource.cs", "HttpMethod.Delete, \"/integrator/keys\""],
  ["dotnet/src/EPostak/EPostakClient.cs", "public ConnectorResource Connector"],
  ["dotnet/src/EPostak/Resources/ConnectorResource.cs", "/connector/preflight"],
  ["dotnet/src/EPostak/Resources/ConnectorResource.cs", "/connector/send"],
  ["dotnet/src/EPostak/Resources/ConnectorResource.cs", "/connector/status/"],
  ["dotnet/src/EPostak/Resources/ConnectorResource.cs", "/connector/inbox"],
  ["dotnet/src/EPostak/Resources/ConnectorResource.cs", "/connector/inbox/{Uri.EscapeDataString(documentId)}"],
  ["dotnet/src/EPostak/Resources/ConnectorResource.cs", "/connector/inbox/{Uri.EscapeDataString(documentId)}/ack"],
  ["dotnet/src/EPostak/Resources/ConnectorResource.cs", "/connector/events"],

  ["java/src/main/java/sk/epostak/sdk/EPostak.java", "private final SapiResource sapi"],
  ["java/src/main/java/sk/epostak/sdk/resources/SapiResource.java", "/sapi/v1/document/send"],
  ["java/src/main/java/sk/epostak/sdk/resources/SapiResource.java", "/sapi/v1/document/receive"],
  ["java/src/main/java/sk/epostak/sdk/resources/SapiResource.java", "/acknowledge"],
  ["java/src/main/java/sk/epostak/sdk/resources/PeppolResource.java", "/company/search"],
  ["java/src/main/java/sk/epostak/sdk/resources/PeppolResource.java", "/peppol/participants/resolve"],
  ["java/src/main/java/sk/epostak/sdk/resources/DocumentsResource.java", "/peppol-documents"],
  ["java/src/main/java/sk/epostak/sdk/resources/AccountResource.java", "/licenses/info"],
  ["java/src/main/java/sk/epostak/sdk/resources/DocumentsResource.java", "evidenceBundle"],
  ["java/src/main/java/sk/epostak/sdk/resources/DocumentsResource.java", "/evidence-bundle"],
  ["java/src/main/java/sk/epostak/sdk/resources/DocumentsResource.java", "statusBatch"],
  ["java/src/main/java/sk/epostak/sdk/resources/DocumentsResource.java", "/documents/status/batch"],
  ["java/src/main/java/sk/epostak/sdk/resources/OutboundResource.java", "getMdn"],
  ["java/src/main/java/sk/epostak/sdk/resources/OutboundResource.java", "/mdn"],
  ["java/src/main/java/sk/epostak/sdk/resources/WebhooksResource.java", "mode"],
  ["java/src/main/java/sk/epostak/sdk/resources/WebhooksResource.java", "count"],
  ["java/src/main/java/sk/epostak/sdk/resources/WebhooksResource.java", "testRunId"],
  ["java/src/main/java/sk/epostak/sdk/resources/WebhooksResource.java", "/webhook-dead-letter"],
  ["java/src/main/java/sk/epostak/sdk/resources/ReportingResource.java", "submissions"],
  ["java/src/main/java/sk/epostak/sdk/resources/ReportingResource.java", "/reporting/submissions"],
  ["java/src/main/java/sk/epostak/sdk/resources/IntegratorResource.java", "IntegratorKeysResource keys"],
  ["java/src/main/java/sk/epostak/sdk/resources/IntegratorResource.java", "/integrator/keys"],
  ["java/src/main/java/sk/epostak/sdk/resources/IntegratorResource.java", "http.delete(\"/integrator/keys\""],
  ["java/src/main/java/sk/epostak/sdk/EPostak.java", "private final ConnectorResource connector"],
  ["java/src/main/java/sk/epostak/sdk/EPostak.java", "public ConnectorResource connector()"],
  ["java/src/main/java/sk/epostak/sdk/resources/ConnectorResource.java", "/connector/preflight"],
  ["java/src/main/java/sk/epostak/sdk/resources/ConnectorResource.java", "/connector/send"],
  ["java/src/main/java/sk/epostak/sdk/resources/ConnectorResource.java", "/connector/status/"],
  ["java/src/main/java/sk/epostak/sdk/resources/ConnectorResource.java", "/connector/inbox"],
  ["java/src/main/java/sk/epostak/sdk/resources/ConnectorResource.java", "/connector/inbox/\" + HttpClient.encode(documentId)"],
  ["java/src/main/java/sk/epostak/sdk/resources/ConnectorResource.java", "/connector/inbox/\" + HttpClient.encode(documentId) + \"/ack"],
  ["java/src/main/java/sk/epostak/sdk/resources/ConnectorResource.java", "/connector/events"],
];

const missing = [];

for (const [relative, needle] of checks) {
  const file = path.join(root, relative);
  if (!fs.existsSync(file)) {
    missing.push(`${relative}: file missing`);
    continue;
  }
  const text = fs.readFileSync(file, "utf8");
  if (!text.includes(needle)) {
    missing.push(`${relative}: missing ${needle}`);
  }
}

if (missing.length > 0) {
  console.error(missing.join("\n"));
  process.exit(1);
}

const forbidden = [
  ["typescript/src/resources/integrator.ts", "POST\", \"/integrator/keys"],
  ["python/src/epostak/resources/integrator.py", "\"POST\", \"/integrator/keys"],
  ["ruby/lib/epostak/resources/integrator.rb", ":post, \"/integrator/keys"],
  ["php/src/Resources/Integrator.php", "'POST', '/integrator/keys"],
  ["dotnet/src/EPostak/Resources/IntegratorResource.cs", "HttpMethod.Post, \"/integrator/keys"],
  ["java/src/main/java/sk/epostak/sdk/resources/IntegratorResource.java", "http.post(\"/integrator/keys"],
];

const forbiddenHits = [];

for (const [relative, needle] of forbidden) {
  const file = path.join(root, relative);
  if (!fs.existsSync(file)) continue;
  const text = fs.readFileSync(file, "utf8");
  if (text.includes(needle)) {
    forbiddenHits.push(`${relative}: forbidden ${needle}`);
  }
}

if (forbiddenHits.length > 0) {
  console.error(forbiddenHits.join("\n"));
  process.exit(1);
}

console.log(`Endpoint coverage OK (${checks.length} checks, ${forbidden.length} forbidden checks)`);
