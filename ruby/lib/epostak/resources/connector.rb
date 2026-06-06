# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Connector workflow endpoints for ERP teams.
    #
    # Connector is a polling-first workflow over the Enterprise API. It uses
    # the same credentials, firm scoping, and documentId as the full
    # Enterprise API.
    class Connector
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
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
        @http.request(:post, "/connector/outbox/#{encode(outbox_id)}/send", body: body, retry_on_failure: true)
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
        @http.request(:post, "/connector/autopilot", body: body)
      end

      # Normalize a loose ERP/customer payload into a Connector lifecycle run.
      #
      # @param body [Hash] Zen input request with customerRef and invoice/customer fields
      # @return [Hash] Autopilot run lifecycle response
      def zen_input(body)
        @http.request(:post, "/connector/zen-input", body: body)
      end

      # Retrieve an Autopilot run by ID.
      #
      # @param autopilot_id [String] Connector Autopilot run ID
      # @return [Hash] Autopilot run lifecycle response
      def get_autopilot_run(autopilot_id)
        @http.request(:get, "/connector/autopilot/#{encode(autopilot_id)}")
      end

      # Send a shadow-validated or staged Autopilot run.
      #
      # @param autopilot_id [String] Connector Autopilot run ID
      # @return [Hash] Autopilot run lifecycle response
      def send_autopilot_run(autopilot_id)
        @http.request(:post, "/connector/autopilot/#{encode(autopilot_id)}/send", body: {}, retry_on_failure: true)
      end

      # List Connector reconciliation items for ERP state sync.
      #
      # @param status [String, nil] exceptions or all
      # @param since [String, nil] ISO 8601 timestamp for incremental sync
      # @return [Hash] Reconciliation items
      def reconcile(status: nil, since: nil)
        @http.request(:get, "/connector/reconcile", query: { status: status, since: since })
      end

      # List Connector-managed customer mailboxes.
      #
      # @return [Hash] Mailbox list response
      def mailboxes
        @http.request(:get, "/connector/mailbox")
      end

      # Repair Connector mailbox state for one customer or all customers.
      #
      # @param body [Hash] Optional request body with customerRef
      # @return [Hash] Repair result
      def repair_mailbox(body = {})
        @http.request(:post, "/connector/mailbox/repair", body: body)
      end

      # Update the managed send policy for a Connector mailbox.
      #
      # @param customer_ref [String] Connector mailbox customer reference
      # @param body [Hash] Send policy request with policy and optional sendAt
      # @return [Hash] Updated mailbox response
      def update_mailbox_send_policy(customer_ref, body)
        @http.request(:patch, "/connector/mailbox/#{encode(customer_ref)}/send-policy", body: body)
      end

      # List Connector sync items for ERP reconciliation cursors.
      #
      # @param customer_ref [String, nil] Optional customer reference filter
      # @param cursor [String, nil] Cursor from a previous sync page
      # @param limit [Integer, nil] Page limit
      # @return [Hash] Sync page
      def sync(customer_ref: nil, cursor: nil, limit: nil)
        @http.request(:get, "/connector/sync", query: { customerRef: customer_ref, cursor: cursor, limit: limit })
      end

      # Retrieve a Connector document lifecycle snapshot.
      #
      # @param document_id [String] Connector document ID
      # @return [Hash] Document lifecycle snapshot
      def get_document(document_id)
        @http.request(:get, "/connector/documents/#{encode(document_id)}")
      end

      # Download a Connector document UBL XML body.
      #
      # @param document_id [String] Connector document ID
      # @return [String] UBL XML
      def get_document_ubl(document_id)
        @http.request_raw(:get, "/connector/documents/#{encode(document_id)}/ubl")
      end

      # Retrieve Connector document delivery evidence.
      #
      # @param document_id [String] Connector document ID
      # @return [Hash] Evidence payload
      def get_document_evidence(document_id)
        @http.request(:get, "/connector/documents/#{encode(document_id)}/evidence")
      end

      # Retrieve the Connector evidence bundle manifest.
      #
      # @param document_id [String] Connector document ID
      # @return [Hash] Evidence bundle manifest
      def get_document_evidence_bundle(document_id)
        @http.request(:get, "/connector/documents/#{encode(document_id)}/evidence-bundle")
      end

      # Execute a pending Connector action.
      #
      # @param action_id [String] Connector action ID
      # @param body [Hash] Optional action request body
      # @return [Hash] Action result
      def run_action(action_id, body = {})
        @http.request(:post, "/connector/actions/#{encode(action_id)}", body: body, retry_on_failure: true)
      end

      private

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
