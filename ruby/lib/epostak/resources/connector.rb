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

      private

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
