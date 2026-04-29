# frozen_string_literal: true

require "erb"
require "openssl"

module EPostak
  # Verify an inbound webhook signature. Uses constant-time comparison to prevent
  # timing attacks. Rejects payloads older than +max_age_ms+ milliseconds to
  # prevent replay attacks.
  #
  # Signature format: +t=<unix_ms>,v1=<hex_hmac_sha512>+
  #
  # @param payload [String] The raw webhook request body
  # @param signature [String] The value of the +X-Epostak-Signature+ header
  # @param secret [String] The webhook signing secret from webhook creation
  # @param max_age_ms [Integer] Maximum acceptable age in milliseconds (default 300_000 = 5 min)
  # @return [Boolean] +true+ if the signature is valid and within the replay window
  #
  # @example
  #   valid = EPostak::Webhook.verify_signature(
  #     payload: request.body.read,
  #     signature: request.headers["X-Epostak-Signature"],
  #     secret: ENV["WEBHOOK_SECRET"]
  #   )
  #   return head :unauthorized unless valid
  module Webhook
    MAX_AGE_MS = 300_000

    # @see EPostak::Webhook
    def self.verify_signature(payload:, signature:, secret:, max_age_ms: MAX_AGE_MS)
      t_part = nil
      v_part = nil
      signature.split(",").each do |segment|
        t_part = segment[2..] if segment.start_with?("t=")
        v_part = segment[3..] if segment.start_with?("v1=")
      end
      return false if t_part.nil? || v_part.nil?

      timestamp = Integer(t_part, 10, exception: false)
      return false if timestamp.nil?

      age = (Time.now.to_f * 1000).to_i - timestamp
      return false if age < 0 || age > max_age_ms

      message = "#{timestamp}.#{payload}"
      computed = OpenSSL::HMAC.hexdigest("SHA512", secret, message)

      OpenSSL.secure_compare(computed, v_part.downcase)
    rescue StandardError
      false
    end
  end

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
      # @param url [String] HTTPS URL to receive webhook POST requests. HTTPS is required; +http://+ URLs are rejected.
      # @param events [Array<String>, nil] Event types to subscribe to (nil = all events).
      #   Available events (7): +document.created+, +document.sent+, +document.received+,
      #   +document.validated+, +document.delivered+, +document.rejected+,
      #   +document.response_received+.
      # @return [Hash] Webhook details { "id", "url", "events", "secret", "isActive", "createdAt" }
      #   — +secret+ is shown only once; store it securely.
      #
      # @param idempotency_key [String, nil] Optional client-chosen
      #   `Idempotency-Key` header. Replays of the same key surface as
      #   `EPostak::Error` with `code == "idempotency_conflict"`.
      #
      # @example
      #   webhook = client.webhooks.create(
      #     url: "https://example.com/webhooks",
      #     events: ["document.received"]
      #   )
      #   puts webhook["secret"] # => Store this securely!
      def create(url:, events: nil, idempotency_key: nil)
        body = { url: url }
        body[:events] = events if events
        @http.request(:post, "/webhooks", body: body, idempotency_key: idempotency_key)
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
      # Returns HTTP 204 No Content on success; the SDK returns +nil+.
      #
      # @param id [String] Webhook UUID to delete
      # @return [nil] Nothing on success (HTTP 204 No Content)
      #
      # @example
      #   client.webhooks.delete("webhook-uuid") # => nil
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
        @http.request(:get, "/webhooks/#{encode(id)}/deliveries", query: params)
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
