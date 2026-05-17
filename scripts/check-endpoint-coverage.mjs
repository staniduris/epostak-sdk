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
  ["typescript/src/resources/documents.ts", "/peppol-documents"],
  ["typescript/src/resources/account.ts", "/licenses/info"],
  ["typescript/src/resources/documents.ts", "evidenceBundle"],
  ["typescript/src/resources/documents.ts", "/evidence-bundle"],
  ["typescript/src/resources/outbound.ts", "getMdn"],
  ["typescript/src/resources/outbound.ts", "/mdn"],

  ["python/src/epostak/client.py", "self.sapi = SapiResource"],
  ["python/src/epostak/resources/sapi.py", "/sapi/v1/document/send"],
  ["python/src/epostak/resources/sapi.py", "/sapi/v1/document/receive"],
  ["python/src/epostak/resources/sapi.py", "/acknowledge"],
  ["python/src/epostak/resources/peppol.py", "/company/search"],
  ["python/src/epostak/resources/documents.py", "/peppol-documents"],
  ["python/src/epostak/resources/account.py", "/licenses/info"],
  ["python/src/epostak/resources/documents.py", "evidence_bundle"],
  ["python/src/epostak/resources/documents.py", "/evidence-bundle"],
  ["python/src/epostak/resources/outbound.py", "get_mdn"],
  ["python/src/epostak/resources/outbound.py", "/mdn"],

  ["ruby/lib/epostak/client.rb", "@sapi"],
  ["ruby/lib/epostak/resources/sapi.rb", "/sapi/v1/document/send"],
  ["ruby/lib/epostak/resources/sapi.rb", "/sapi/v1/document/receive"],
  ["ruby/lib/epostak/resources/sapi.rb", "/acknowledge"],
  ["ruby/lib/epostak/resources/peppol.rb", "/company/search"],
  ["ruby/lib/epostak/resources/documents.rb", "/peppol-documents"],
  ["ruby/lib/epostak/resources/account.rb", "/licenses/info"],
  ["ruby/lib/epostak/resources/documents.rb", "evidence_bundle"],
  ["ruby/lib/epostak/resources/documents.rb", "/evidence-bundle"],
  ["ruby/lib/epostak/resources/outbound.rb", "get_mdn"],
  ["ruby/lib/epostak/resources/outbound.rb", "/mdn"],

  ["php/src/EPostak.php", "public Sapi $sapi"],
  ["php/src/Resources/Sapi.php", "/sapi/v1/document/send"],
  ["php/src/Resources/Sapi.php", "/sapi/v1/document/receive"],
  ["php/src/Resources/Sapi.php", "/acknowledge"],
  ["php/src/Resources/Peppol.php", "/company/search"],
  ["php/src/Resources/Documents.php", "/peppol-documents"],
  ["php/src/Resources/Account.php", "/licenses/info"],
  ["php/src/Resources/Documents.php", "evidenceBundle"],
  ["php/src/Resources/Documents.php", "/evidence-bundle"],
  ["php/src/Resources/Outbound.php", "getMdn"],
  ["php/src/Resources/Outbound.php", "/mdn"],

  ["dotnet/src/EPostak/EPostakClient.cs", "public SapiResource Sapi"],
  ["dotnet/src/EPostak/Resources/SapiResource.cs", "/sapi/v1/document/send"],
  ["dotnet/src/EPostak/Resources/SapiResource.cs", "/sapi/v1/document/receive"],
  ["dotnet/src/EPostak/Resources/SapiResource.cs", "/acknowledge"],
  ["dotnet/src/EPostak/Resources/PeppolResource.cs", "/company/search"],
  ["dotnet/src/EPostak/Resources/DocumentsResource.cs", "/peppol-documents"],
  ["dotnet/src/EPostak/Resources/AccountResource.cs", "/licenses/info"],
  ["dotnet/src/EPostak/Resources/DocumentsResource.cs", "EvidenceBundleAsync"],
  ["dotnet/src/EPostak/Resources/DocumentsResource.cs", "/evidence-bundle"],
  ["dotnet/src/EPostak/Resources/OutboundResource.cs", "GetMdnAsync"],
  ["dotnet/src/EPostak/Resources/OutboundResource.cs", "/mdn"],

  ["java/src/main/java/sk/epostak/sdk/EPostak.java", "private final SapiResource sapi"],
  ["java/src/main/java/sk/epostak/sdk/resources/SapiResource.java", "/sapi/v1/document/send"],
  ["java/src/main/java/sk/epostak/sdk/resources/SapiResource.java", "/sapi/v1/document/receive"],
  ["java/src/main/java/sk/epostak/sdk/resources/SapiResource.java", "/acknowledge"],
  ["java/src/main/java/sk/epostak/sdk/resources/PeppolResource.java", "/company/search"],
  ["java/src/main/java/sk/epostak/sdk/resources/DocumentsResource.java", "/peppol-documents"],
  ["java/src/main/java/sk/epostak/sdk/resources/AccountResource.java", "/licenses/info"],
  ["java/src/main/java/sk/epostak/sdk/resources/DocumentsResource.java", "evidenceBundle"],
  ["java/src/main/java/sk/epostak/sdk/resources/DocumentsResource.java", "/evidence-bundle"],
  ["java/src/main/java/sk/epostak/sdk/resources/OutboundResource.java", "getMdn"],
  ["java/src/main/java/sk/epostak/sdk/resources/OutboundResource.java", "/mdn"],
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

console.log(`Endpoint coverage OK (${checks.length} checks)`);
