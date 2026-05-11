# frozen_string_literal: true

module EPostak
  # Represents a received (inbound) document returned by the Pull API.
  #
  # All attributes mirror the JSON keys returned by +GET /inbound/documents+
  # and +GET /inbound/documents/{id}+. Use {.from_hash} to build from a
  # parsed response hash.
  #
  # @example
  #   page = client.inbound.list
  #   docs = page["documents"].map { |h| EPostak::InboundDocument.from_hash(h) }
  #   docs.each { |d| puts "#{d.id} from #{d.sender_peppol_id}" }
  InboundDocument = Struct.new(
    :id,
    :kind,
    :sender_peppol_id,
    :sender_name,
    :received_at,
    :client_acked_at,
    :status,
    keyword_init: true
  ) do
    # Build from a parsed API response hash (string or symbol keys).
    # @param hash [Hash]
    # @return [InboundDocument]
    def self.from_hash(hash)
      h = hash.transform_keys { |k| k.to_s }
      new(
        id:               h["id"],
        kind:             h["kind"],
        sender_peppol_id: h["senderPeppolId"],
        sender_name:      h["senderName"],
        received_at:      h["receivedAt"],
        client_acked_at:  h["clientAckedAt"],
        status:           h["status"],
      )
    end
  end

  # Represents a sent (outbound) document returned by the Pull API.
  #
  # Use {.from_hash} to build from a parsed response hash.
  OutboundDocument = Struct.new(
    :id,
    :kind,
    :invoice_number,
    :recipient_peppol_id,
    :recipient_name,
    :status,
    :business_status,
    :sent_at,
    :delivered_at,
    :attempt_history,
    keyword_init: true
  ) do
    # @param hash [Hash]
    # @return [OutboundDocument]
    def self.from_hash(hash)
      h = hash.transform_keys { |k| k.to_s }
      new(
        id:                  h["id"],
        kind:                h["kind"],
        invoice_number:      h["invoiceNumber"],
        recipient_peppol_id: h["recipientPeppolId"],
        recipient_name:      h["recipientName"],
        status:              h["status"],
        business_status:     h["businessStatus"],
        sent_at:             h["sentAt"],
        delivered_at:        h["deliveredAt"],
        attempt_history:     h["attemptHistory"],
      )
    end
  end

  # Represents a single event in the outbound event stream
  # (+GET /outbound/events+).
  OutboundEvent = Struct.new(
    :id,
    :document_id,
    :type,
    :actor,
    :detail,
    :meta,
    :occurred_at,
    keyword_init: true
  ) do
    # @param hash [Hash]
    # @return [OutboundEvent]
    def self.from_hash(hash)
      h = hash.transform_keys { |k| k.to_s }
      new(
        id:          h["id"],
        document_id: h["document_id"] || h["documentId"],
        type:        h["type"],
        actor:       h["actor"],
        detail:      h["detail"],
        meta:        h["meta"],
        occurred_at: h["occurred_at"] || h["occurredAt"],
      )
    end
  end

  # Represents a single webhook delivery attempt.
  #
  # Includes the optional +idempotency_key+ field that the server echoes
  # back when the original webhook trigger carried one.
  WebhookDelivery = Struct.new(
    :id,
    :webhook_id,
    :event,
    :status,
    :status_code,
    :response_time_ms,
    :created_at,
    :idempotency_key,
    keyword_init: true
  ) do
    # @param hash [Hash]
    # @return [WebhookDelivery]
    def self.from_hash(hash)
      h = hash.transform_keys { |k| k.to_s }
      new(
        id:               h["id"],
        webhook_id:       h["webhookId"],
        event:            h["event"],
        status:           h["status"],
        status_code:      h["statusCode"],
        response_time_ms: h["responseTime"] || h["responseTimeMs"],
        created_at:       h["createdAt"],
        idempotency_key:  h["idempotencyKey"] || h["idempotency_key"],
      )
    end
  end
end
