# Enterprise Core migration release candidates

Prepared from base `e602ee5bf19bc43fa074e6910e34d30b5a722978` on 2026-07-16. These artifacts were local release candidates only and did not authorize registry publication.

Permanent migration guide: <https://epostak.sk/api/docs/enterprise/migrations/enterprise-core-distillation>

| Ecosystem | RC version | Local artifact SHA-256 |
| --- | --- | --- |
| TypeScript | `4.1.1-rc.1` | `c64cd75aba47d3adcd8056a3d2b2474428d9afe928ec3e0070ef95e516a534d8` |
| Python | `1.1.1rc1` | `80c365e908c59bc1584745d5303b411e49753036afcfa81837821c822dd4abb6` |
| PHP | `1.1.2-RC1` | `97b1cd8ffb9e7e64fbfd58fa96f040f009161cc4d82ffa27d3112afaedc7ff47` |
| Ruby | `1.1.1.rc1` | `830e85fe7460712a69edb5f97f5a3fe88b4c2af2014ee1120baacdbeead0d69a` |
| Java | `1.1.1-RC1` | `a24bff4391a8420be0c791ce790646c6b44374e9523249260bfca70714005ad2` |
| .NET | `1.1.1-rc.1` | `8725f4f27c909c5e1a23cd96018be5adce38623b80e329f94f6ec8432e8154da` |

All six packages were built and then installed/imported from their packaged artifacts in separate clean temporary projects. The language suites passed: TypeScript 46 tests, Python 69 tests, PHP contract scripts, Ruby 60 examples, Java 35 tests, and .NET 39 tests.

The SDK adapters keep every public class, method, signature, namespace, and Enterprise object identity. Deprecated method names delegate internally to `payloads`, `events`, and `supportPacket`; they do not call retired URLs. The nine unused alias URLs were removed from Enterprise API 1.7.0 on 2026-07-20. Enterprise `X-Firm-Id`, Connector `customerRef`, and SAPI participant scope code was not merged or redirected.

`scripts/check-endpoint-coverage.mjs` loads frozen and current OpenAPI documents, verifies the nine canonical operations for security, required parameters and bodies, response contracts, and transitively referenced schemas, and asserts that all nine retired routes are absent from the current contract.
