package sk.epostak.sdk.models;

/**
 * Query parameters for {@code GET /outbound/documents}. All fields are optional.
 * Build via {@link #builder()}.
 *
 * @param since          ISO 8601 lower bound on document creation
 * @param limit          page size (1-500, default 100)
 * @param kind           document kind filter, e.g. {@code "invoice"}, {@code "self_billing"}
 * @param status         transport status filter, e.g. {@code "delivered"}, {@code "failed"}
 * @param businessStatus business-level status from Invoice Response, e.g. {@code "AP"}
 * @param recipient      filter by recipient Peppol ID (prefix match)
 * @param cursor         opaque cursor from a previous page's {@code nextCursor}
 */
public record OutboundListParams(
        String since,
        Integer limit,
        String kind,
        String status,
        String businessStatus,
        String recipient,
        String cursor
) {

    /**
     * Empty params — equivalent to calling {@code GET /outbound/documents} with no filters.
     *
     * @return an {@link OutboundListParams} with every field {@code null}
     */
    public static OutboundListParams empty() {
        return new OutboundListParams(null, null, null, null, null, null, null);
    }

    /**
     * Create a fresh builder.
     *
     * @return a new builder instance
     */
    public static Builder builder() {
        return new Builder();
    }

    /** Builder for {@link OutboundListParams}. */
    public static final class Builder {
        private String since;
        private Integer limit;
        private String kind;
        private String status;
        private String businessStatus;
        private String recipient;
        private String cursor;

        private Builder() {}

        /** @param since ISO 8601 lower bound on creation timestamp @return this builder */
        public Builder since(String since) { this.since = since; return this; }

        /** @param limit page size (1-500) @return this builder */
        public Builder limit(Integer limit) { this.limit = limit; return this; }

        /** @param kind document kind filter @return this builder */
        public Builder kind(String kind) { this.kind = kind; return this; }

        /** @param status transport status filter @return this builder */
        public Builder status(String status) { this.status = status; return this; }

        /** @param businessStatus Invoice Response business status filter @return this builder */
        public Builder businessStatus(String businessStatus) { this.businessStatus = businessStatus; return this; }

        /** @param recipient recipient Peppol ID filter (prefix match) @return this builder */
        public Builder recipient(String recipient) { this.recipient = recipient; return this; }

        /** @param cursor opaque pagination cursor @return this builder */
        public Builder cursor(String cursor) { this.cursor = cursor; return this; }

        /**
         * Build the params record.
         *
         * @return the constructed {@link OutboundListParams}
         */
        public OutboundListParams build() {
            return new OutboundListParams(since, limit, kind, status, businessStatus, recipient, cursor);
        }
    }
}
