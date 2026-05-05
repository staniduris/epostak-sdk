# frozen_string_literal: true

require "openssl"

module EPostak
  # Verify an ePošťák webhook payload using HMAC-SHA256 with timing-safe compare.
  #
  # The server sends two separate headers:
  #   X-Webhook-Signature: sha256=<hex>
  #   X-Webhook-Timestamp: <unix_seconds>
  #
  # The signed string is +"#{timestamp}.#{raw_body}"+ — computed on the bytes
  # exactly as received off the wire. Do NOT re-serialize parsed JSON.
  #
  # @param payload [String] Raw request body bytes, as received off the wire
  # @param signature [String] Value of the +X-Webhook-Signature+ header (e.g. "sha256=abc123")
  # @param timestamp [String, Integer] Value of the +X-Webhook-Timestamp+ header (unix seconds)
  # @param secret [String] The webhook signing secret captured at creation time
  # @param tolerance_seconds [Integer] Maximum age of the signature in seconds.
  #   Defaults to 300 (5 minutes). Set 0 to disable timestamp check (not
  #   recommended in production).
  # @return [Hash] +{ valid: Boolean, reason: Symbol|nil, timestamp: Integer|nil }+
  #
  # @example Sinatra
  #   post "/webhooks/epostak" do
  #     raw = request.body.read
  #     result = EPostak.verify_webhook_signature(
  #       payload:   raw,
  #       signature: request.env["HTTP_X_WEBHOOK_SIGNATURE"].to_s,
  #       timestamp: request.env["HTTP_X_WEBHOOK_TIMESTAMP"].to_s,
  #       secret:    ENV["EPOSTAK_WEBHOOK_SECRET"]
  #     )
  #     halt 400, "bad signature: #{result[:reason]}" unless result[:valid]
  #     event = JSON.parse(raw)
  #     # process event...
  #     status 204
  #   end
  def self.verify_webhook_signature(payload:, signature:, timestamp:, secret:, tolerance_seconds: 300)
    if signature.nil? || signature.empty?
      return { valid: false, reason: :missing_header, timestamp: nil }
    end

    if timestamp.nil? || timestamp.to_s.empty?
      return { valid: false, reason: :missing_timestamp, timestamp: nil }
    end

    # Parse "sha256=<hex>" — reject anything with a different algorithm prefix
    unless signature.start_with?("sha256=")
      return { valid: false, reason: :unsupported_algorithm, timestamp: nil }
    end

    candidate_hex = signature[7..]

    ts_int = begin
      Integer(timestamp.to_s, 10)
    rescue ArgumentError, TypeError
      nil
    end
    return { valid: false, reason: :malformed_timestamp, timestamp: nil } if ts_int.nil?

    if tolerance_seconds.positive?
      now = Time.now.to_i
      if (now - ts_int).abs > tolerance_seconds
        return { valid: false, reason: :timestamp_outside_tolerance, timestamp: ts_int }
      end
    end

    expected = OpenSSL::HMAC.hexdigest("SHA256", secret, "#{ts_int}.#{payload}")

    return { valid: false, reason: :signature_mismatch, timestamp: ts_int } if candidate_hex.length != expected.length

    if secure_compare(candidate_hex, expected)
      { valid: true, reason: nil, timestamp: ts_int }
    else
      { valid: false, reason: :signature_mismatch, timestamp: ts_int }
    end
  end

  # @api private
  def self.secure_compare(a, b)
    if OpenSSL.respond_to?(:fixed_length_secure_compare) && a.bytesize == b.bytesize
      begin
        return OpenSSL.fixed_length_secure_compare(a, b)
      rescue StandardError
        # Fall through to manual constant-time compare
      end
    end

    return false if a.bytesize != b.bytesize

    res = 0
    a.bytes.zip(b.bytes) { |x, y| res |= x ^ y }
    res.zero?
  end
end
