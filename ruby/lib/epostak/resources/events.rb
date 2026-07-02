# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Preferred event pull/ack facade over the webhook queue.
    class Events
      def initialize(http)
        @http = http
      end

      def pull(limit: nil, event_type: nil)
        normalize_pull_response(
          @http.request(:get, "/events/pull", query: { limit: limit, event_type: event_type })
        )
      end

      def ack(event_id)
        @http.request(:post, "/events/#{encode(event_id)}/ack")
      end

      def batch_ack(event_ids)
        @http.request(:post, "/events/batch-ack", body: { event_ids: event_ids })
      end

      private

      def normalize_pull_response(response)
        if response.is_a?(Hash) && !response.key?("items") && response["events"].is_a?(Array)
          response.merge("items" => response["events"])
        else
          response
        end
      end

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
