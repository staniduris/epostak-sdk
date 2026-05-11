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

  # Canonical webhook event type strings (v1 contract).
  #
  # Eight events are defined:
  # - +document.created+       — document ingested into the system
  # - +document.sent+          — outbound AS4 send succeeded
  # - +document.received+      — inbound AS4 message ingested
  # - +document.validated+     — document passed schematron validation
  # - +document.delivered+     — receiving AP confirmed delivery
  # - +document.delivery_failed+ — all retry attempts exhausted
  # - +document.rejected+      — buyer/AP produced a rejection (MLR/IMR)
  # - +document.response_received+ — buyer InvoiceResponse received
  WEBHOOK_EVENTS = %w[
    document.created
    document.sent
    document.received
    document.validated
    document.delivered
    document.delivery_failed
    document.rejected
    document.response_received
  ].freeze

  # Common envelope for every v1 webhook payload — both push (POST body)
  # and pull (queue item +payload+ field).
  #
  # @example Build from a parsed push request body
  #   payload = EPostak::WebhookPayload.from_hash(JSON.parse(request.body.read))
  #   puts payload.event          # => "document.sent"
  #   puts payload.data.document_id
  WebhookPayload = Struct.new(
    :event,
    :event_version,
    :webhook_id,
    :webhook_event_id,
    :timestamp,
    :data,
    keyword_init: true
  ) do
    # Build from a parsed JSON hash (string or symbol keys).
    # The +data+ field is converted into a {WebhookPayloadData} struct.
    # @param hash [Hash]
    # @return [WebhookPayload]
    def self.from_hash(hash)
      h = hash.transform_keys { |k| k.to_s }
      new(
        event:             h["event"],
        event_version:     h["event_version"],
        webhook_id:        h["webhook_id"],
        webhook_event_id:  h["webhook_event_id"],
        timestamp:         h["timestamp"],
        data:              h["data"].is_a?(Hash) ? EPostak::WebhookPayloadData.from_hash(h["data"]) : h["data"],
      )
    end
  end

  # Business-data payload carried inside {WebhookPayload#data}.
  #
  # Common fields (+document_id+, +direction+, +doctype_key+, +status+,
  # +previous_status+) are always present. All other fields are nullable/
  # optional and depend on the event type.
  WebhookPayloadData = Struct.new(
    # Always present
    :document_id,
    :direction,
    :doctype_key,
    :status,
    :previous_status,
    # Often present (billing events)
    :document_number,
    :total_amount,
    :currency,
    :issue_date,
    :due_date,
    :sender_peppol_id,
    :receiver_peppol_id,
    # document.sent / document.received / document.delivered
    :sent_at,
    :received_at,
    :delivered_at,
    :as4_message_id,
    # document.rejected
    :rejected_at,
    # document.response_received
    :responded_at,
    # document.rejected / document.response_received
    :response_code,
    :response_reason,
    :responder,
    # document.delivery_failed
    :failure_reason,
    :attempts,
    keyword_init: true
  ) do
    # Build from a parsed JSON hash (string or symbol keys).
    # @param hash [Hash]
    # @return [WebhookPayloadData]
    def self.from_hash(hash)
      h = hash.transform_keys { |k| k.to_s }
      new(
        document_id:         h["document_id"],
        direction:           h["direction"],
        doctype_key:         h["doctype_key"],
        status:              h["status"],
        previous_status:     h["previous_status"],
        document_number:     h["document_number"],
        total_amount:        h["total_amount"],
        currency:            h["currency"],
        issue_date:          h["issue_date"],
        due_date:            h["due_date"],
        sender_peppol_id:    h["sender_peppol_id"],
        receiver_peppol_id:  h["receiver_peppol_id"],
        sent_at:             h["sent_at"],
        received_at:         h["received_at"],
        delivered_at:        h["delivered_at"],
        as4_message_id:      h["as4_message_id"],
        rejected_at:         h["rejected_at"],
        responded_at:        h["responded_at"],
        response_code:       h["response_code"],
        response_reason:     h["response_reason"],
        responder:           h["responder"],
        failure_reason:      h["failure_reason"],
        attempts:            h["attempts"],
      )
    end
  end

  # A webhook subscription as returned by GET /webhooks and GET /webhooks/{id}.
  #
  # +url+ is +nil+ for pull-only subscriptions (consumed via the queue).
  Webhook = Struct.new(
    :id,
    :url,
    :events,
    :is_active,
    :failed_attempts,
    :created_at,
    keyword_init: true
  ) do
    # @param hash [Hash]
    # @return [Webhook]
    def self.from_hash(hash)
      h = hash.transform_keys { |k| k.to_s }
      new(
        id:              h["id"],
        url:             h["url"],
        events:          h["events"],
        is_active:       h["isActive"],
        failed_attempts: h["failedAttempts"],
        created_at:      h["createdAt"],
      )
    end
  end

  # Webhook details including the signing secret — returned once at creation.
  # +url+ is +nil+ for pull-only subscriptions.
  WebhookDetail = Struct.new(
    :id,
    :url,
    :events,
    :secret,
    :is_active,
    :created_at,
    keyword_init: true
  ) do
    # @param hash [Hash]
    # @return [WebhookDetail]
    def self.from_hash(hash)
      h = hash.transform_keys { |k| k.to_s }
      new(
        id:         h["id"],
        url:        h["url"],
        events:     h["events"],
        secret:     h["secret"],
        is_active:  h["isActive"],
        created_at: h["createdAt"],
      )
    end
  end

  # Request body for creating a new webhook subscription.
  # +url+ is optional: omit (or pass +nil+) for a pull-only subscription.
  CreateWebhookRequest = Struct.new(
    :url,
    :events,
    :is_active,
    keyword_init: true
  ) do
    # Serialise for API submission. +url+ is always included (nil = pull-only).
    # @return [Hash]
    def to_api_hash
      h = { url: url }
      h[:events]   = events    if events
      h[:isActive] = is_active unless is_active.nil?
      h
    end
  end

  # Request body for updating an existing webhook subscription.
  # Omit fields to leave them unchanged. Pass +url: nil+ to switch to pull-only.
  UpdateWebhookRequest = Struct.new(
    :url,
    :events,
    :is_active,
    keyword_init: true
  ) do
    # Serialise for API submission. Only sends fields that were explicitly set.
    # @return [Hash]
    def to_api_hash
      h = {}
      h[:url]      = url      if members.include?(:url)
      h[:events]   = events   if events
      h[:isActive] = is_active unless is_active.nil?
      h
    end
  end

  # A single event returned from the pull queue (one item in "items" array).
  # +payload+ is typed as {WebhookPayload}.
  WebhookQueueItem = Struct.new(
    :event_id,
    :firm_id,
    :event,
    :created_at,
    :payload,
    keyword_init: true
  ) do
    # @param hash [Hash]
    # @return [WebhookQueueItem]
    def self.from_hash(hash)
      h = hash.transform_keys { |k| k.to_s }
      new(
        event_id:   h["event_id"],
        firm_id:    h["firm_id"],
        event:      h["event"],
        created_at: h["created_at"],
        payload:    h["payload"].is_a?(Hash) ? EPostak::WebhookPayload.from_hash(h["payload"]) : h["payload"],
      )
    end
  end

  # A single item from the cross-firm queue (integrator +pull_all+ endpoint).
  # +payload+ is typed as {WebhookPayload}.
  WebhookQueueAllEvent = Struct.new(
    :event_id,
    :firm_id,
    :event,
    :created_at,
    :payload,
    keyword_init: true
  ) do
    # @param hash [Hash]
    # @return [WebhookQueueAllEvent]
    def self.from_hash(hash)
      h = hash.transform_keys { |k| k.to_s }
      new(
        event_id:   h["event_id"],
        firm_id:    h["firm_id"],
        event:      h["event"],
        created_at: h["created_at"],
        payload:    h["payload"].is_a?(Hash) ? EPostak::WebhookPayload.from_hash(h["payload"]) : h["payload"],
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
