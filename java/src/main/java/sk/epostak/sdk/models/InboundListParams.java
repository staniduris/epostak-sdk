package sk.epostak.sdk.models;

/**
 * Query parameters for {@code GET /inbound/documents}. All fields are optional.
 * Build via {@link #builder()}.
 *
 * @param since   ISO 8601 timestamp — only return documents received after this
 * @param limit   page size (1-500, default 100)
 * @param kind    filter by document kind, e.g. {@code "invoice"}, {@code "credit_note"}
 * @param sender  filter by sender Peppol ID (prefix match)
 * @param cursor  opaque cursor from a previous page's {@code nextCursor}
 */
public record InboundListParams(
        String since,
        Integer limit,
        String kind,
        String sender,
        String cursor
) {

    /**
     * Empty params — equivalent to calling {@code GET /inbound/documents} with no filters.
     *
     * @return an {@link InboundListParams} with every field {@code null}
     */
    public static InboundListParams empty() {
        return new InboundListParams(null, null, null, null, null);
    }

    /**
     * Create a fresh builder.
     *
     * @return a new builder instance
     */
    public static Builder builder() {
        return new Builder();
    }

    /** Builder for {@link InboundListParams}. */
    public static final class Builder {
        private String since;
        private Integer limit;
        private String kind;
        private String sender;
        private String cursor;

        private Builder() {}

        /** @param since ISO 8601 lower bound on {@code receivedAt} @return this builder */
        public Builder since(String since) { this.since = since; return this; }

        /** @param limit page size (1-500) @return this builder */
        public Builder limit(Integer limit) { this.limit = limit; return this; }

        /** @param kind document kind filter, e.g. {@code "invoice"} @return this builder */
        public Builder kind(String kind) { this.kind = kind; return this; }

        /** @param sender sender Peppol ID filter (prefix match) @return this builder */
        public Builder sender(String sender) { this.sender = sender; return this; }

        /** @param cursor opaque pagination cursor @return this builder */
        public Builder cursor(String cursor) { this.cursor = cursor; return this; }

        /**
         * Build the params record.
         *
         * @return the constructed {@link InboundListParams}
         */
        public InboundListParams build() {
            return new InboundListParams(since, limit, kind, sender, cursor);
        }
    }
}
