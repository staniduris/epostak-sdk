# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Resource for managing webhook subscriptions and the pull queue.
    #
    # Webhooks notify your server about document events (sent, received, validated).
    # Choose between push webhooks (server receives HTTPS POST) or the pull queue
    # (your code polls for events).
    #
    # @example Create a push webhook
    #   webhook = client.webhooks.create(
    #     url: "https://example.com/webhooks/epostak",
    #     events: ["document.received", "document.sent"]
    #   )
    #   # Store webhook["secret"] for HMAC verification
    class Webhooks
      # @return [Resources::WebhookQueue] Sub-resource for the pull queue (polling-based event consumption)
      attr_reader :queue

      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http  = http
        @queue = WebhookQueue.new(http)
      end

      # Create a new webhook subscription. Returns the HMAC-SHA256 signing secret
      # which is only available at creation time -- store it securely.
      #
      # @param url [String] HTTPS URL to receive webhook POST requests
      # @param events [Array<String>, nil] Event types to subscribe to (nil = all events).
      #   Available events: "document.received", "document.sent", "document.validated",
      #   "document.status_changed", "document.response_received"
      # @return [Hash] Webhook details including the one-time "secret" signing key
      #
      # @example
      #   webhook = client.webhooks.create(
      #     url: "https://example.com/webhooks",
      #     events: ["document.received"]
      #   )
      #   puts webhook["secret"] # => Store this securely!
      def create(url:, events: nil)
        body = { url: url }
        body[:events] = events if events
        @http.request(:post, "/webhooks", body: body)
      end

      # List all webhook subscriptions for the current account.
      #
      # @return [Hash] Response with "data" array of webhook objects
      #
      # @example
      #   response = client.webhooks.list
      #   response["data"].each { |w| puts "#{w['url']} (active: #{w['isActive']})" }
      def list
        @http.request(:get, "/webhooks")
      end

      # Get a webhook subscription by ID, including recent delivery history.
      # Use the delivery history to debug failed webhook deliveries.
      #
      # @param id [String] Webhook UUID
      # @return [Hash] Webhook details with "deliveries" array
      #
      # @example
      #   webhook = client.webhooks.get("webhook-uuid")
      #   failed = webhook["deliveries"].select { |d| d["status"] == "failed" }
      def get(id)
        @http.request(:get, "/webhooks/#{encode(id)}")
      end

      # Update a webhook subscription. Use this to change the URL, event filter,
      # or pause/resume the webhook.
      #
      # @param id [String] Webhook UUID
      # @param params [Hash] Fields to update (url, events, isActive)
      # @return [Hash] The updated webhook
      #
      # @example Pause a webhook
      #   client.webhooks.update("webhook-uuid", isActive: false)
      #
      # @example Change URL and events
      #   client.webhooks.update("webhook-uuid",
      #     url: "https://new-url.com/webhooks",
      #     events: ["document.received", "document.validated"]
      #   )
      def update(id, **params)
        @http.request(:patch, "/webhooks/#{encode(id)}", body: params)
      end

      # Delete a webhook subscription. Stops all future deliveries.
      #
      # @param id [String] Webhook UUID to delete
      # @return [Hash] Confirmation with "deleted" => true
      #
      # @example
      #   client.webhooks.delete("webhook-uuid")
      def delete(id)
        @http.request(:delete, "/webhooks/#{encode(id)}")
      end

      # Send a test event to a webhook endpoint.
      #
      # @param id [String] Webhook UUID to test
      # @param event [String, nil] Event type to simulate (e.g. "document.created"). Nil uses server default.
      # @return [Hash] Test result with "success", "statusCode", "responseTime", "webhookId", "event", and optional "error"
      #
      # @example
      #   result = client.webhooks.test("webhook-uuid", event: "document.received")
      #   puts result["success"] ? "OK" : result["error"]
      def test(id, event: nil)
        body = {}
        body[:event] = event if event
        @http.request(:post, "/webhooks/#{encode(id)}/test", body: body)
      end

      # Get paginated delivery history for a webhook.
      #
      # @param id [String] Webhook UUID
      # @param limit [Integer, nil] Max deliveries to return (1-100, default 20)
      # @param offset [Integer, nil] Number of deliveries to skip (default 0)
      # @param status [String, nil] Filter by status: "SUCCESS", "FAILED", "PENDING", "RETRYING"
      # @param event [String, nil] Filter by event type
      # @return [Hash] Paginated response with "deliveries", "total", "limit", "offset"
      #
      # @example
      #   result = client.webhooks.deliveries("webhook-uuid", status: "FAILED", limit: 50)
      #   result["deliveries"].each { |d| puts "#{d['event']}: #{d['status']}" }
      def deliveries(id, **params)
        @http.request(:get, "/webhooks/#{encode(id)}/deliveries", params: params)
      end

      # Rotate a webhook's HMAC-SHA256 signing secret. Issues a fresh secret
      # and invalidates the previous one immediately. The new secret is
      # returned ONCE — store it right away; there is no way to retrieve it
      # later. In-flight deliveries signed with the old secret will no longer
      # verify on the receiving side. Non-destructive alternative to
      # delete+recreate when a secret leaks.
      #
      # @param id [String] Webhook UUID whose secret to rotate
      # @return [Hash] { "id" => ..., "secret" => ..., "message" => ... } — secret only shown once
      #
      # @example
      #   res = client.webhooks.rotate_secret("webhook-uuid")
      #   ENV["EPOSTAK_WEBHOOK_SECRET"] = res["secret"]
      def rotate_secret(id)
        @http.request(:post, "/webhooks/#{encode(id)}/rotate-secret")
      end

      private

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
