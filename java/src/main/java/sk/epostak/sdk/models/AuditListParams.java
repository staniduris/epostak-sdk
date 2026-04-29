package sk.epostak.sdk.models;

/**
 * Query parameters for {@code GET /audit} — per-firm audit feed. All fields
 * are optional. Build via {@link #builder()}.
 *
 * @param event      exact match on the {@code event} field (e.g. {@code "jwt.issued"})
 * @param actorType  exact match on the {@code actor_type} field
 * @param since      ISO 8601 timestamp — only return rows newer than or equal to this
 * @param until      ISO 8601 timestamp — only return rows older than or equal to this
 * @param cursor     opaque cursor from a previous page's {@code nextCursor}
 * @param limit      page size (1-100). Defaults to 20 server-side when {@code null}.
 */
public record AuditListParams(
        String event,
        AuditActorType actorType,
        String since,
        String until,
        String cursor,
        Integer limit
) {
    /**
     * Empty params — equivalent to calling {@code GET /audit} with no filters.
     *
     * @return an {@link AuditListParams} with every field {@code null}
     */
    public static AuditListParams empty() {
        return new AuditListParams(null, null, null, null, null, null);
    }

    /**
     * Create a fresh builder for stepwise construction.
     *
     * @return a new builder
     */
    public static Builder builder() {
        return new Builder();
    }

    /** Builder for {@link AuditListParams}. */
    public static final class Builder {
        private String event;
        private AuditActorType actorType;
        private String since;
        private String until;
        private String cursor;
        private Integer limit;

        private Builder() {}

        /** @param event exact match on the {@code event} field @return this builder */
        public Builder event(String event) { this.event = event; return this; }
        /** @param actorType actor type filter @return this builder */
        public Builder actorType(AuditActorType actorType) { this.actorType = actorType; return this; }
        /** @param since ISO 8601 lower bound @return this builder */
        public Builder since(String since) { this.since = since; return this; }
        /** @param until ISO 8601 upper bound @return this builder */
        public Builder until(String until) { this.until = until; return this; }
        /** @param cursor opaque cursor from a previous page @return this builder */
        public Builder cursor(String cursor) { this.cursor = cursor; return this; }
        /** @param limit page size (1-100) @return this builder */
        public Builder limit(Integer limit) { this.limit = limit; return this; }

        /**
         * Build the params record.
         *
         * @return the constructed {@link AuditListParams}
         */
        public AuditListParams build() {
            return new AuditListParams(event, actorType, since, until, cursor, limit);
        }
    }
}
