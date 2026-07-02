import type { AccountResource } from "./account.js";
import type { AuditResource } from "./audit.js";
import type { AuthResource } from "./auth.js";
import type { BoxResource } from "./box.js";
import type { ConnectorResource } from "./connector.js";
import type { DocumentsResource, InboxResource } from "./documents.js";
import type { EventsResource } from "./events.js";
import type { ExtractResource } from "./extract.js";
import type { FirmsResource } from "./firms.js";
import type { InboundResource } from "./inbound.js";
import type { IntegratorResource } from "./integrator.js";
import type { OutboundResource } from "./outbound.js";
import type { PayloadsResource } from "./payloads.js";
import type { PeppolResource } from "./peppol.js";
import type { ReportingResource } from "./reporting.js";
import type { WebhooksResource } from "./webhooks.js";

export class EnterprisePullResource {
  constructor(
    readonly inbound: InboundResource,
    readonly outbound: OutboundResource,
  ) {}
}

export interface EnterpriseResourceConfig {
  auth: AuthResource;
  box: BoxResource;
  audit: AuditResource;
  documents: DocumentsResource;
  firms: FirmsResource;
  peppol: PeppolResource;
  webhooks: WebhooksResource;
  reporting: ReportingResource;
  extract: ExtractResource;
  account: AccountResource;
  integrator: IntegratorResource;
  connector: ConnectorResource;
  payloads: PayloadsResource;
  events: EventsResource;
  inbound: InboundResource;
  outbound: OutboundResource;
}

export class EnterpriseResource {
  readonly auth: AuthResource;
  readonly box: BoxResource;
  readonly audit: AuditResource;
  readonly documents: DocumentsResource;
  readonly inbox: InboxResource;
  readonly firms: FirmsResource;
  readonly peppol: PeppolResource;
  readonly webhooks: WebhooksResource;
  readonly reporting: ReportingResource;
  readonly extract: ExtractResource;
  readonly account: AccountResource;
  readonly integrator: IntegratorResource;
  readonly connector: ConnectorResource;
  readonly payloads: PayloadsResource;
  readonly events: EventsResource;
  readonly pull: EnterprisePullResource;

  constructor(resources: EnterpriseResourceConfig) {
    this.auth = resources.auth;
    this.box = resources.box;
    this.audit = resources.audit;
    this.documents = resources.documents;
    this.inbox = resources.documents.inbox;
    this.firms = resources.firms;
    this.peppol = resources.peppol;
    this.webhooks = resources.webhooks;
    this.reporting = resources.reporting;
    this.extract = resources.extract;
    this.account = resources.account;
    this.integrator = resources.integrator;
    this.connector = resources.connector;
    this.payloads = resources.payloads;
    this.events = resources.events;
    this.pull = new EnterprisePullResource(resources.inbound, resources.outbound);
  }
}
