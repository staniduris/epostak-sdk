# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Sub-resource for the webhook pull queue -- an alternative to push webhooks.
    # Use the pull queue when your server cannot receive inbound HTTPS requests.
    # Events accumulate in the queue and must be acknowledged after processing.
    #
    # Accessed via +client.webhooks.queue+.
    #
    # @example Poll-based event consumption loop
    #   loop do
    #     response = client.webhooks.queue.pull(limit: 50)
    #     break if response["events"].empty?
    #
    #     ids = response["events"].map { |item| process(item); item["event_id"] }
    #     client.webhooks.queue.batch_ack(ids)
    #     break if response["count"] == 0
    #   end
    class WebhookQueue
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # Pull unacknowledged events from the webhook queue.
      # Events remain in the queue until explicitly acknowledged via +ack+ or +batch_ack+.
      #
      # @param limit [Integer, nil] Maximum number of events to return
      # @param event_type [String, nil] Filter by event type (e.g. "document.received")
      # @return [Hash] Response with "events" array (each has event_id, firm_id, event, created_at, payload) and "count"
      #
      # @example
      #   response = client.webhooks.queue.pull(limit: 20, event_type: "document.received")
      #   response["events"].each { |item| puts item["event"] }
      def pull(limit: nil, event_type: nil)
        query = { limit: limit, event_type: event_type }
        @http.request(:get, "/webhook-queue", query: query)
      end

      # Acknowledge (remove) a single event from the queue after processing.
      #
      # @param event_id [String] The event ID to acknowledge
      # @return [Hash] { "acknowledged" => true }
      #
      # @example
      #   result = client.webhooks.queue.ack("event-uuid")
      #   puts result["acknowledged"] # => true
      def ack(event_id)
        @http.request(:delete, "/webhook-queue/#{encode(event_id)}")
      end

      # Acknowledge (remove) multiple events from the queue in a single request.
      #
      # @param event_ids [Array<String>] Array of event IDs to acknowledge
      # @return [Hash] { "acknowledged" => N } with count of acknowledged events
      #
      # @example
      #   response = client.webhooks.queue.pull(limit: 50)
      #   ids = response["events"].map { |i| i["event_id"] }
      #   result = client.webhooks.queue.batch_ack(ids)
      #   puts "Acknowledged: #{result['acknowledged']}"
      def batch_ack(event_ids)
        @http.request(:post, "/webhook-queue/batch-ack", body: { event_ids: event_ids })
      end

      # Pull events across all managed firms (integrator endpoint).
      # Only available with integrator API keys (+sk_int_*+).
      # Use the +since+ parameter for cursor-based polling.
      #
      # @param limit [Integer, nil] Maximum number of events to return
      # @param since [String, nil] ISO 8601 timestamp for cursor-based polling
      # @return [Hash] Response with "events" array and "count"
      #
      # @example
      #   response = client.webhooks.queue.pull_all(since: "2026-04-11T00:00:00Z", limit: 200)
      #   response["events"].each { |e| puts "#{e['firm_id']}: #{e['type']}" }
      def pull_all(limit: nil, since: nil)
        query = { limit: limit, since: since }
        @http.request(:get, "/webhook-queue/all", query: query)
      end

      # Acknowledge (remove) multiple events from the cross-firm queue (integrator endpoint).
      # Only available with integrator API keys (+sk_int_*+).
      #
      # @param event_ids [Array<String>] Array of event IDs to acknowledge
      # @return [Hash] Response with "acknowledged" count
      #
      # @example
      #   response = client.webhooks.queue.pull_all(limit: 100)
      #   ids = response["events"].map { |e| e["event_id"] }
      #   result = client.webhooks.queue.batch_ack_all(ids)
      #   puts "Acknowledged: #{result['acknowledged']}"
      def batch_ack_all(event_ids)
        @http.request(:post, "/webhook-queue/all/batch-ack", body: { event_ids: event_ids })
      end

      private

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
