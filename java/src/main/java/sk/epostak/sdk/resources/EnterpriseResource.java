package sk.epostak.sdk.resources;

import sk.epostak.sdk.EPostak;

/** Workflow-first namespace for Enterprise /api/v1 resources. */
public final class EnterpriseResource {
    private final EPostak client;
    private final EnterprisePullResource pull;

    public EnterpriseResource(EPostak client) {
        this.client = client;
        this.pull = new EnterprisePullResource(client.inbound(), client.outbound());
    }

    public DocumentsResource documents() { return client.documents(); }
    public InboxResource inbox() { return client.documents().inbox(); }
    public FirmsResource firms() { return client.firms(); }
    public PeppolResource peppol() { return client.peppol(); }
    public WebhooksResource webhooks() { return client.webhooks(); }
    public ReportingResource reporting() { return client.reporting(); }
    public ExtractResource extract() { return client.extract(); }
    public AccountResource account() { return client.account(); }
    public AuthResource auth() { return client.auth(); }
    public BoxResource box() { return client.box(); }
    public ConnectorResource connector() { return client.connector(); }
    public AuditResource audit() { return client.audit(); }
    public IntegratorResource integrator() { return client.integrator(); }
    public EnterprisePullResource pull() { return pull; }
}
