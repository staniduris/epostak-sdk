# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # ePošťák Box durable execution layer for staged, scheduled, and retryable
    # Peppol dispatch.
    class Box
      def initialize(http)
        @http = http
      end

      def list(status: nil, direction: nil, limit: nil, offset: nil)
        @http.request(
          :get,
          "/box/items",
          query: { status: status, direction: direction, limit: limit, offset: offset },
        )
      end

      def create(payload_xml:, scheduled_for: nil, external_id: nil, metadata: nil)
        @http.request(
          :post,
          "/box/items",
          body: {
            payloadXml: payload_xml,
            scheduledFor: scheduled_for,
            externalId: external_id,
            metadata: metadata,
          }.compact,
        )
      end

      def get(item_id)
        @http.request(:get, "/box/items/#{encode(item_id)}")
      end

      def schedule(item_id, scheduled_for:)
        @http.request(
          :post,
          "/box/items/#{encode(item_id)}/schedule",
          body: { scheduledFor: scheduled_for },
        )
      end

      def send_now(item_id)
        @http.request(:post, "/box/items/#{encode(item_id)}/send-now")
      end

      def retry(item_id)
        @http.request(:post, "/box/items/#{encode(item_id)}/retry")
      end

      def cancel(item_id)
        @http.request(:post, "/box/items/#{encode(item_id)}/cancel")
      end

      private

      def encode(value)
        ERB::Util.url_encode(value.to_s)
      end
    end
  end
end
