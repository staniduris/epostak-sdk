# frozen_string_literal: true

require "erb"
require "digest"

module EPostak
  module Resources
    CONNECTOR_TRIM_PATTERN = /\A[\u0009-\u000D\u0020\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF]+|[\u0009-\u000D\u0020\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF]+\z/u.freeze
    CONNECTOR_INVOICE_RESPONSE_STATUSES = %w[
      received
      in_process
      under_query
      conditionally_accepted
      rejected
      accepted
      paid
    ].freeze

    def self.connector_trim_string(value)
      value.to_s.gsub(CONNECTOR_TRIM_PATTERN, "")
    end

    # Stable bounded key for a length-prefixed UTF-8 tuple.
    def self.connector_document_idempotency_key(customer_ref, external_id)
      customer = connector_trim_string(customer_ref).encode(Encoding::UTF_8).b
      external = connector_trim_string(external_id).encode(Encoding::UTF_8).b
      tuple = [customer.bytesize].pack("N") + customer + [external.bytesize].pack("N") + external
      "connector:v1:#{Digest::SHA256.hexdigest(tuple)}"
    end

    def self.connector_idempotency_key(value)
      bytes = value.to_s.encode(Encoding::UTF_8).bytesize
      if connector_trim_string(value).empty? || bytes > 255
        raise ArgumentError, "Connector idempotency key must be 1-255 UTF-8 bytes"
      end
      value
    end

    # Copy a caller payload while emitting each SDK-owned wire key exactly once.
    def self.connector_payload(body, replacements)
      payload = body.dup
      replacements.each do |key, value|
        payload.delete(key)
        payload.delete(key.to_s)
        payload[key] = value
      end
      payload
    end

    # Connector workflow endpoints for ERP teams.
    #
    # The primary API is customers.for_customer(ref).documents and .events.
    # Direct preflight/send/outbox/inbox/Autopilot/sync methods are supported
    # compatibility aliases for the same methods under advanced.
    class Connector
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
        @documents = ConnectorDocuments.new(self)
        @customers = ConnectorCustomers.new(self)
        @webhook = ConnectorWebhook.new(http)
        @advanced = ConnectorAdvanced.new(self)
      end

      attr_reader :customers, :documents, :webhook, :advanced

      # Autopilot submit compatibility alias retained with its original staged
      # semantics. Use customer.documents.send/stage for business documents.
      def submit_document(body)
        mode = body[:mode] || body["mode"] || "stage"
        payload = Resources.connector_payload(body, mode: mode)
        autopilot(payload)
      end

      def submit_customer_document(customer_ref, body, delivery: "send", idempotency_key: nil)
        raise ArgumentError, "Connector customerRef is required" if Resources.connector_trim_string(customer_ref).empty?

        external_id = Resources.connector_trim_string(body[:externalId] || body["externalId"])
        raise ArgumentError, "Connector externalId is required" if external_id.empty?
        raise ArgumentError, "Connector number is required" if (body[:number] || body["number"]).to_s.strip.empty?

        recipient = body[:recipient] || body["recipient"]
        unless recipient.is_a?(Hash) && !(recipient[:country] || recipient["country"]).to_s.strip.empty?
          raise ArgumentError, "Connector recipient.country is required"
        end
        recipient_ids = %i[companyId taxId vatId networkId].map do |key|
          recipient[key] || recipient[key.to_s]
        end
        if recipient_ids.none? { |value| !value.to_s.strip.empty? }
          raise ArgumentError, "Connector recipient requires companyId, taxId, vatId, or networkId"
        end
        lines = body[:lines] || body["lines"]
        raise ArgumentError, "Connector lines must contain at least one item" unless lines.is_a?(Array) && !lines.empty?

        normalized_customer_ref = Resources.connector_trim_string(customer_ref)
        payload = Resources.connector_payload(
          body,
          customerRef: normalized_customer_ref,
          externalId: external_id,
          delivery: delivery,
        )
        key = idempotency_key.nil? ?
          Resources.connector_document_idempotency_key(normalized_customer_ref, external_id) :
          Resources.connector_idempotency_key(idempotency_key)
        @http.request(
          :post,
          "/connector/documents",
          body: payload,
          idempotency_key: key,
          retry_on_failure: true,
          retry_network_errors: true,
          omit_firm_id: true,
        )
      end

      # Validate receiver reachability and payload readiness before send.
      #
      # @param body [Hash] Connector preflight payload
      # @return [Hash] Repair report and readiness result
      def preflight(body)
        @http.request(:post, "/connector/preflight", body: body)
      end

      # Send an ERP document payload through Connector.
      #
      # @param body [Hash] Connector send payload
      # @param idempotency_key [String, nil] Optional Idempotency-Key header
      # @return [Hash] Send response with documentId and status
      def send_document(body, idempotency_key: nil)
        @http.request(:post, "/connector/send", body: body, idempotency_key: idempotency_key)
      end

      # Get Connector status for a document ID.
      #
      # @param document_id [String] Document UUID
      # @return [Hash] Connector status response
      def status(document_id)
        @http.request(:get, "/connector/status/#{encode(document_id)}")
      end

      # List Connector inbox documents with cursor pagination.
      #
      # @param cursor [String, nil] Opaque cursor from the previous page
      # @param limit [Integer, nil] Maximum documents to return
      # @return [Hash] Page with documents, nextCursor, and hasMore
      def inbox(cursor: nil, limit: nil)
        @http.request(:get, "/connector/inbox", query: { cursor: cursor, limit: limit })
      end

      # Retrieve a single Connector inbox document.
      #
      # @param document_id [String] Document UUID
      # @return [Hash] Connector inbox document
      def get_inbox_document(document_id)
        @http.request(:get, "/connector/inbox/#{encode(document_id)}")
      end

      # Acknowledge a Connector inbox document as processed.
      #
      # @param document_id [String] Document UUID
      # @return [Hash] Connector ack response
      def ack(document_id)
        @http.request(:post, "/connector/inbox/#{encode(document_id)}/ack", body: {})
      end

      # List Connector polling events with cursor pagination.
      #
      # @param cursor [String, nil] Opaque cursor from the previous page
      # @param limit [Integer, nil] Maximum events to return
      # @return [Hash] Page with events, nextCursor, and hasMore
      def events(cursor: nil, limit: nil)
        @http.request(:get, "/connector/events", query: { cursor: cursor, limit: limit })
      end

      # Customer-scoped business events use a distinct response shape with
      # `state`; firm-scoped legacy events above expose `status`.
      def customer_events(customer_ref, cursor: nil, limit: nil)
        @http.request(
          :get,
          "/connector/events",
          query: { customerRef: customer_ref.to_s.strip, cursor: cursor, limit: limit },
          omit_firm_id: true,
        )
      end

      # Stage one or more ERP invoices without immediate Peppol delivery.
      #
      # @param body [Hash] Connector outbox staging payload
      # @return [Hash] Stage response with items and repair reports
      def stage_outbox(body)
        @http.request(:post, "/connector/outbox", body: body)
      end

      # List staged Connector outbox items.
      #
      # @param status [String, nil] ready, blocked, scheduled, sending, sent, failed, cancelled
      # @param limit [Integer, nil] Maximum items to return
      # @param offset [Integer, nil] Offset for simple polling lists
      # @return [Hash] Connector outbox list response
      def list_outbox(status: nil, limit: nil, offset: nil)
        @http.request(:get, "/connector/outbox", query: { status: status, limit: limit, offset: offset })
      end

      # Retrieve a single Connector outbox item.
      #
      # @param outbox_id [String] Connector outbox item ID
      # @return [Hash] Connector outbox item
      def get_outbox_item(outbox_id)
        @http.request(:get, "/connector/outbox/#{encode(outbox_id)}")
      end

      # Send one staged outbox item through the Connector workflow.
      #
      # @param outbox_id [String] Connector outbox item ID
      # @param force [Boolean, nil] Send before scheduledFor when true
      # @return [Hash] Connector outbox item or blocked repair report
      def send_outbox_item(outbox_id, force: nil)
        body = {}
        body[:force] = force unless force.nil?
        @http.request(:post, "/connector/outbox/#{encode(outbox_id)}/send", body: body, retry_on_failure: true, retry_network_errors: true)
      end

      # Send ready, failed, or due scheduled outbox items in a batch.
      #
      # @param ids [Array<String>, nil] Optional list of outbox item IDs
      # @param limit [Integer, nil] Max items when ids is omitted
      # @param force [Boolean, nil] Send before scheduledFor when true
      # @return [Hash] Per-item batch send result
      def send_outbox_batch(ids: nil, limit: nil, force: nil)
        body = {}
        body[:ids] = ids unless ids.nil?
        body[:limit] = limit unless limit.nil?
        body[:force] = force unless force.nil?
        @http.request(:post, "/connector/outbox/send", body: body)
      end

      # Cancel a staged outbox item before it is sent.
      #
      # @param outbox_id [String] Connector outbox item ID
      # @return [Hash] Cancelled Connector outbox item
      def cancel_outbox_item(outbox_id)
        @http.request(:delete, "/connector/outbox/#{encode(outbox_id)}")
      end

      # Start a managed Connector Autopilot lifecycle run.
      #
      # @param body [Hash] Autopilot request with mode, payload, and optional IDs
      # @return [Hash] Autopilot run lifecycle response
      def autopilot(body)
        @http.request(:post, "/connector/autopilot", body: body, omit_firm_id: true)
      end

      # Map a saved Connector Mapper template input into preview, stage, or send.
      #
      # @param body [Hash] Mapper request with templateKey and source payload
      # @return [Hash] Mapping preview, checklist, or Autopilot result
      def mapper(body)
        @http.request(:post, "/connector/mapper", body: body, omit_firm_id: true)
      end

      # Normalize a loose ERP/customer payload into a Connector lifecycle run.
      #
      # @param body [Hash] Zen input request with customerRef and invoice/customer fields
      # @return [Hash] Autopilot run lifecycle response
      def zen_input(body)
        @http.request(:post, "/connector/zen-input", body: body, omit_firm_id: true)
      end

      # Retrieve an Autopilot run by ID.
      #
      # @param autopilot_id [String] Connector Autopilot run ID
      # @return [Hash] Autopilot run lifecycle response
      def get_autopilot_run(autopilot_id)
        @http.request(:get, "/connector/autopilot/#{encode(autopilot_id)}", omit_firm_id: true)
      end

      # Send a shadow-validated or staged Autopilot run.
      #
      # @param autopilot_id [String] Connector Autopilot run ID
      # @return [Hash] Autopilot run lifecycle response
      def send_autopilot_run(autopilot_id)
        @http.request(:post, "/connector/autopilot/#{encode(autopilot_id)}/send", body: {}, retry_on_failure: true, retry_network_errors: true, omit_firm_id: true)
      end

      # List Connector reconciliation items for ERP state sync.
      #
      # @param status [String, nil] exceptions or all
      # @param since [String, nil] ISO 8601 timestamp for incremental sync
      # @return [Hash] Reconciliation items
      def reconcile(status: nil, since: nil)
        @http.request(:get, "/connector/reconcile", query: { status: status, since: since }, omit_firm_id: true)
      end

      # List Connector-managed customer mailboxes.
      #
      # @return [Hash] Mailbox list response
      def mailboxes
        @http.request(:get, "/connector/mailbox", omit_firm_id: true)
      end

      # Repair Connector mailbox state for one customer or all customers.
      #
      # @param body [Hash] Optional request body with customerRef
      # @return [Hash] Repair result
      def repair_mailbox(body = {})
        @http.request(:post, "/connector/mailbox/repair", body: body, omit_firm_id: true)
      end

      # Update the managed send policy for a Connector mailbox.
      #
      # @param customer_ref [String] Connector mailbox customer reference
      # @param body [Hash] Send policy request with policy and optional sendAt
      # @return [Hash] Updated mailbox response
      def update_mailbox_send_policy(customer_ref, body)
        @http.request(:patch, "/connector/mailbox/#{encode(customer_ref)}/send-policy", body: body, omit_firm_id: true)
      end

      # List Connector sync items for ERP reconciliation cursors.
      #
      # @param customer_ref [String, nil] Optional customer reference filter
      # @param cursor [String, nil] Cursor from a previous sync page
      # @param limit [Integer, nil] Page limit
      # @return [Hash] Sync page
      def sync(customer_ref: nil, cursor: nil, limit: nil)
        @http.request(:get, "/connector/sync", query: { customerRef: customer_ref, cursor: cursor, limit: limit }, omit_firm_id: true)
      end

      # Retrieve a Connector document lifecycle snapshot.
      #
      # @param document_id [String] Connector document ID
      # @return [Hash] Document lifecycle snapshot
      def get_document(document_id, customer_ref: nil)
        @http.request(:get, "/connector/documents/#{encode(document_id)}", query: { customerRef: customer_ref }, omit_firm_id: true)
      end

      def list_customer_documents(customer_ref, direction: nil, state: nil, type: nil, created_after: nil, cursor: nil, limit: nil)
        @http.request(
          :get,
          "/connector/documents",
          query: {
            customerRef: customer_ref,
            direction: direction,
            state: state,
            type: type,
            createdAfter: created_after,
            cursor: cursor,
            limit: limit,
          },
          omit_firm_id: true,
        )
      end

      def acknowledge_document(document_id, reference, customer_ref: nil)
        raise ArgumentError, "Connector reference is required" if reference.to_s.strip.empty?

        @http.request(
          :post,
          "/connector/documents/#{encode(document_id)}/acknowledge",
          body: { reference: reference },
          query: { customerRef: customer_ref },
          retry_on_failure: true,
          retry_network_errors: true,
          omit_firm_id: true,
        )
      end

      def respond_document(document_id, customer_ref, body)
        normalized_document_id = document_id.to_s.strip
        normalized_customer_ref = Resources.connector_trim_string(customer_ref)
        status = body[:status] || body["status"]
        note_present = body.key?(:note) || body.key?("note")
        note = body.key?(:note) ? body[:note] : body["note"]
        raise ArgumentError, "Connector documentId is required" if normalized_document_id.empty?
        raise ArgumentError, "Connector customerRef is required" if normalized_customer_ref.empty?
        unless CONNECTOR_INVOICE_RESPONSE_STATUSES.include?(status)
          raise ArgumentError, "Invalid Connector response status"
        end
        if note_present && !note.is_a?(String)
          raise ArgumentError, "Connector response note must be a string"
        end

        payload = { status: status }
        payload[:note] = note if note_present
        @http.request(
          :post,
          "/connector/documents/#{encode(normalized_document_id)}/respond",
          query: { customerRef: normalized_customer_ref },
          body: payload,
          retry_on_failure: true,
          retry_network_errors: true,
          omit_firm_id: true,
        )
      end

      # Send a previously staged customer document.
      def send_customer_document(document_id, customer_ref: nil)
        raise ArgumentError, "Connector documentId is required" if document_id.to_s.strip.empty?

        @http.request(
          :post,
          "/connector/documents/#{encode(document_id)}/send",
          query: { customerRef: customer_ref },
          retry_on_failure: true,
          retry_network_errors: true,
          omit_firm_id: true,
        )
      end

      # Cancel a staged customer document before delivery starts.
      def cancel_customer_document(document_id, customer_ref: nil)
        raise ArgumentError, "Connector documentId is required" if document_id.to_s.strip.empty?

        @http.request(
          :post,
          "/connector/documents/#{encode(document_id)}/cancel",
          query: { customerRef: customer_ref },
          retry_on_failure: true,
          retry_network_errors: true,
          omit_firm_id: true,
        )
      end

      # Download a Connector document UBL XML body.
      #
      # @param document_id [String] Connector document ID
      # @return [String] UBL XML
      def get_document_ubl(document_id, customer_ref: nil)
        @http.request_raw(:get, "/connector/documents/#{encode(document_id)}/ubl", query: { customerRef: customer_ref }, omit_firm_id: true)
      end

      # Retrieve Connector document delivery evidence.
      #
      # @param document_id [String] Connector document ID
      # @return [Hash] Evidence payload
      def get_document_evidence(document_id, customer_ref: nil)
        @http.request(:get, "/connector/documents/#{encode(document_id)}/evidence", query: { customerRef: customer_ref }, omit_firm_id: true)
      end

      # Retrieve the Connector evidence bundle manifest.
      #
      # @param document_id [String] Connector document ID
      # @return [Hash] Evidence bundle manifest
      def get_document_evidence_bundle(document_id, customer_ref: nil)
        @http.request(:get, "/connector/documents/#{encode(document_id)}/evidence-bundle", query: { customerRef: customer_ref }, omit_firm_id: true)
      end

      # Retrieve the Connector support packet manifest.
      def get_document_support_packet(document_id, customer_ref: nil)
        @http.request(:get, "/connector/documents/#{encode(document_id)}/support-packet", query: { customerRef: customer_ref }, omit_firm_id: true)
      end

      # Execute a pending Connector action.
      #
      # @param action_id [String] Connector action ID
      # @param body [Hash] Optional action request body
      # @return [Hash] Action result
      def run_action(action_id, body = {})
        @http.request(:post, "/connector/actions/#{encode(action_id)}", body: body, retry_on_failure: true, retry_network_errors: true, omit_firm_id: true)
      end

      private

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end

    # One global Connector webhook configuration per integrator.
    class ConnectorWebhook
      def initialize(http)
        @http = http
      end

      # Get the integrator's Connector webhook configuration.
      def get
        @http.request(:get, "/connector/webhook", omit_firm_id: true)
      end

      # Create or replace the integrator's Connector webhook.
      def configure(url, events = nil)
        normalized_url = url.to_s.strip
        raise ArgumentError, "Connector webhook URL is required" if normalized_url.empty?

        body = { url: normalized_url }
        body[:events] = events.to_a unless events.nil?
        @http.request(:put, "/connector/webhook", body: body, omit_firm_id: true)
      end

      # Delete the integrator's Connector webhook configuration.
      def delete
        @http.request(:delete, "/connector/webhook", omit_firm_id: true)
      end

      # Rotate and return the Connector webhook signing secret.
      def rotate_secret
        @http.request(:post, "/connector/webhook/rotate-secret", omit_firm_id: true)
      end

      # Send a canonical test event for one approved customer.
      def test(customer_ref)
        normalized_customer_ref = Resources.connector_trim_string(customer_ref)
        raise ArgumentError, "Connector customerRef is required" if normalized_customer_ref.empty?

        @http.request(
          :post,
          "/connector/webhook/test",
          body: { customerRef: normalized_customer_ref },
          omit_firm_id: true,
        )
      end

      # List Connector webhook delivery attempts.
      def deliveries(cursor: nil, limit: nil, status: nil)
        @http.request(
          :get,
          "/connector/webhook/deliveries",
          query: { cursor: cursor, limit: limit, status: status&.to_s&.upcase },
          omit_firm_id: true,
        )
      end
    end

    # Protocol-oriented staging queue kept outside the primary document API.
    class ConnectorAdvancedOutbox
      def initialize(connector)
        @connector = connector
      end

      def stage(body)
        @connector.stage_outbox(body)
      end

      def list(status: nil, limit: nil, offset: nil)
        @connector.list_outbox(status: status, limit: limit, offset: offset)
      end

      def get(outbox_id)
        @connector.get_outbox_item(outbox_id)
      end

      def send(outbox_id, force: nil)
        @connector.send_outbox_item(outbox_id, force: force)
      end

      def send_batch(ids: nil, limit: nil, force: nil)
        @connector.send_outbox_batch(ids: ids, limit: limit, force: force)
      end

      def cancel(outbox_id)
        @connector.cancel_outbox_item(outbox_id)
      end
    end

    # Advanced and legacy Connector workflows.
    #
    # New integrations should start with
    # connector.customers.for_customer(ref).documents and .events.
    class ConnectorAdvanced
      attr_reader :outbox, :documents

      def initialize(connector)
        @connector = connector
        @outbox = ConnectorAdvancedOutbox.new(connector)
        @documents = connector.documents
      end

      def preflight(body)
        @connector.preflight(body)
      end

      def send_document(body, idempotency_key: nil)
        @connector.send_document(body, idempotency_key: idempotency_key)
      end

      def status(document_id)
        @connector.status(document_id)
      end

      def inbox(cursor: nil, limit: nil)
        @connector.inbox(cursor: cursor, limit: limit)
      end

      def get_inbox_document(document_id)
        @connector.get_inbox_document(document_id)
      end

      def ack(document_id)
        @connector.ack(document_id)
      end

      def events(cursor: nil, limit: nil)
        @connector.events(cursor: cursor, limit: limit)
      end

      def autopilot(body)
        @connector.autopilot(body)
      end

      def mapper(body)
        @connector.mapper(body)
      end

      def zen_input(body)
        @connector.zen_input(body)
      end

      def get_autopilot_run(autopilot_id)
        @connector.get_autopilot_run(autopilot_id)
      end

      def send_autopilot_run(autopilot_id)
        @connector.send_autopilot_run(autopilot_id)
      end

      def reconcile(status: nil, since: nil)
        @connector.reconcile(status: status, since: since)
      end

      def mailboxes
        @connector.mailboxes
      end

      def repair_mailbox(body = {})
        @connector.repair_mailbox(body)
      end

      def update_mailbox_send_policy(customer_ref, body)
        @connector.update_mailbox_send_policy(customer_ref, body)
      end

      def sync(customer_ref: nil, cursor: nil, limit: nil)
        @connector.sync(customer_ref: customer_ref, cursor: cursor, limit: limit)
      end

      def run_action(action_id, body = {})
        @connector.run_action(action_id, body)
      end
    end

    def self.connector_with_customer_ref(customer_ref, body)
      customer_ref = Resources.connector_trim_string(customer_ref)
      current = body[:customerRef] || body["customerRef"]
      if current && Resources.connector_trim_string(current) != customer_ref
        raise ArgumentError, "Connector customerRef conflicts with scoped customer"
      end

      Resources.connector_payload(body, customerRef: customer_ref)
    end

    class ConnectorCustomers
      def initialize(connector)
        @connector = connector
      end

      def for_customer(customer_ref)
        raise ArgumentError, "Connector customerRef is required" if Resources.connector_trim_string(customer_ref).empty?

        ConnectorCustomer.new(@connector, Resources.connector_trim_string(customer_ref))
      end
    end

    class ConnectorCustomer
      attr_reader :documents, :events, :advanced, :mailbox

      def initialize(connector, customer_ref)
        @connector = connector
        @customer_ref = customer_ref
        @documents = ConnectorCustomerDocuments.new(connector, customer_ref)
        @events = ConnectorCustomerEvents.new(connector, customer_ref)
        @advanced = ConnectorCustomerAdvanced.new(connector, customer_ref)
        # Supported compatibility alias. Use customer.advanced.mailbox.
        @mailbox = @advanced.mailbox
      end

      # Autopilot submit compatibility alias retained with staged semantics.
      def submit_document(body)
        scoped = Resources.connector_with_customer_ref(@customer_ref, body)
        mode = scoped[:mode] || scoped["mode"] || "stage"
        @connector.autopilot(Resources.connector_payload(scoped, mode: mode))
      end

      def autopilot(body)
        @advanced.autopilot(body)
      end

      def mapper(body)
        @advanced.mapper(body)
      end

      def zen_input(body)
        @advanced.zen_input(body)
      end

      def sync(cursor: nil, limit: nil)
        @advanced.sync(cursor: cursor, limit: limit)
      end
    end

    # Advanced helpers for one manually approved Connector customer.
    class ConnectorCustomerAdvanced
      attr_reader :documents

      attr_reader :mailbox

      def initialize(connector, customer_ref)
        @connector = connector
        @customer_ref = customer_ref
        @documents = ConnectorDocuments.new(connector, customer_ref)
        @mailbox = ConnectorCustomerMailbox.new(connector, customer_ref)
      end

      def autopilot(body)
        @connector.advanced.autopilot(Resources.connector_with_customer_ref(@customer_ref, body))
      end

      def mapper(body)
        execute = body[:execute] || body["execute"]
        if execute && execute.to_s != "preview"
          raise ArgumentError, "Connector Mapper only supports preview normalization"
        end
        preview = Resources.connector_payload(body, execute: "preview")
        @connector.advanced.mapper(Resources.connector_with_customer_ref(@customer_ref, preview))
      end

      def zen_input(body)
        @connector.advanced.zen_input(Resources.connector_with_customer_ref(@customer_ref, body))
      end

      def sync(cursor: nil, limit: nil)
        @connector.advanced.sync(customer_ref: @customer_ref, cursor: cursor, limit: limit)
      end
    end

    class ConnectorDocuments
      def initialize(connector, customer_ref = nil)
        @connector = connector
        @customer_ref = customer_ref
      end

      def get(document_id)
        @connector.get_document(document_id, customer_ref: @customer_ref)
      end

      def respond(document_id, customer_ref, body)
        @connector.respond_document(document_id, customer_ref, body)
      end

      def ubl(document_id)
        @connector.get_document_ubl(document_id, customer_ref: @customer_ref)
      end

      def evidence(document_id)
        @connector.get_document_evidence(document_id, customer_ref: @customer_ref)
      end

      def evidence_bundle(document_id)
        @connector.get_document_evidence_bundle(document_id, customer_ref: @customer_ref)
      end

      def support_packet(document_id)
        @connector.get_document_support_packet(document_id, customer_ref: @customer_ref)
      end
    end

    class ConnectorCustomerDocuments < ConnectorDocuments
      def initialize(connector, customer_ref)
        super(connector, customer_ref)
      end

      def get(document_id)
        @connector.get_document(document_id, customer_ref: @customer_ref)
      end

      def respond(document_id, body)
        @connector.respond_document(document_id, @customer_ref, body)
      end

      def ubl(document_id)
        @connector.get_document_ubl(document_id, customer_ref: @customer_ref)
      end

      def evidence(document_id)
        @connector.get_document_evidence(document_id, customer_ref: @customer_ref)
      end

      def evidence_bundle(document_id)
        @connector.get_document_evidence_bundle(document_id, customer_ref: @customer_ref)
      end

      def support_packet(document_id)
        @connector.get_document_support_packet(document_id, customer_ref: @customer_ref)
      end

      def send(body, idempotency_key: nil)
        @connector.submit_customer_document(
          @customer_ref,
          body,
          delivery: "send",
          idempotency_key: idempotency_key,
        )
      end

      def stage(body, idempotency_key: nil)
        @connector.submit_customer_document(
          @customer_ref,
          body,
          delivery: "stage",
          idempotency_key: idempotency_key,
        )
      end

      def list(**filters)
        @connector.list_customer_documents(@customer_ref, **filters)
      end

      def acknowledge(document_id, reference)
        @connector.acknowledge_document(document_id, reference, customer_ref: @customer_ref)
      end

      def send_document(document_id)
        @connector.send_customer_document(document_id, customer_ref: @customer_ref)
      end

      def cancel_document(document_id)
        @connector.cancel_customer_document(document_id, customer_ref: @customer_ref)
      end
    end

    class ConnectorCustomerEvents
      def initialize(connector, customer_ref)
        @connector = connector
        @customer_ref = customer_ref
      end

      def list(cursor: nil, limit: nil)
        @connector.customer_events(@customer_ref, cursor: cursor, limit: limit)
      end
    end

    class ConnectorCustomerMailbox
      def initialize(connector, customer_ref)
        @connector = connector
        @customer_ref = customer_ref
      end

      def repair
        @connector.repair_mailbox(customerRef: @customer_ref)
      end

      def update_send_policy(body)
        @connector.update_mailbox_send_policy(@customer_ref, body)
      end
    end
  end
end
