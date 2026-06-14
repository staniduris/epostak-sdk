package sk.epostak.sdk.resources;

/** Cursor-based Enterprise pull APIs. */
public final class EnterprisePullResource {
    private final InboundResource inbound;
    private final OutboundResource outbound;

    EnterprisePullResource(InboundResource inbound, OutboundResource outbound) {
        this.inbound = inbound;
        this.outbound = outbound;
    }

    public InboundResource inbound() { return inbound; }
    public OutboundResource outbound() { return outbound; }
}
