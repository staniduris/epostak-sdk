namespace EPostak.Resources;

/// <summary>Workflow-first namespace for Enterprise <c>/api/v1/*</c> resources.</summary>
public sealed class EnterpriseResource
{
    public AuthResource Auth { get; }
    public AuditResource Audit { get; }
    public DocumentsResource Documents { get; }
    public InboxResource Inbox { get; }
    public FirmsResource Firms { get; }
    public PeppolResource Peppol { get; }
    public WebhooksResource Webhooks { get; }
    public ReportingResource Reporting { get; }
    public ExtractResource Extract { get; }
    public AccountResource Account { get; }
    public IntegratorResource Integrator { get; }
    public ConnectorResource Connector { get; }
    public EnterprisePullResource Pull { get; }

    internal EnterpriseResource(EPostakClient client)
    {
        Auth = client.Auth;
        Audit = client.Audit;
        Documents = client.Documents;
        Inbox = client.Documents.Inbox;
        Firms = client.Firms;
        Peppol = client.Peppol;
        Webhooks = client.Webhooks;
        Reporting = client.Reporting;
        Extract = client.Extract;
        Account = client.Account;
        Integrator = client.Integrator;
        Connector = client.Connector;
        Pull = new EnterprisePullResource(client.Inbound, client.Outbound);
    }
}

public sealed class EnterprisePullResource
{
    public InboundResource Inbound { get; }
    public OutboundResource Outbound { get; }

    internal EnterprisePullResource(InboundResource inbound, OutboundResource outbound)
    {
        Inbound = inbound;
        Outbound = outbound;
    }
}
