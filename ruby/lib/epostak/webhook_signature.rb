# frozen_string_literal: true

require "openssl"

module EPostak
  # Verify an ePošťák webhook payload using HMAC-SHA256 with timing-safe compare.
  #
  # Header format: `t=<unix_seconds>,v1=<hex_signature>`. Multiple `v1=`
  # signatures may appear (during secret rotation); any of them passing is
  # sufficient.
  #
  # The signed string is `"#{t}.#{raw_body}"`, hex-encoded HMAC-SHA256, computed
  # on the bytes exactly as received off the wire — do NOT re-serialize the
  # parsed JSON, the round-trip will reorder keys and mutate whitespace.
  #
  # @param payload [String] Raw request body, as bytes received off the wire
  # @param signature_header [String] Value of the `X-Epostak-Signature` header
  # @param secret [String] The webhook signing secret captured at creation time
  # @param tolerance_seconds [Integer] Maximum age of the signature in seconds.
  #   Defaults to 300 (5 minutes). Set 0 to disable the timestamp check (not
  #   recommended in production).
  # @return [Hash] `{ valid: Boolean, reason: Symbol|nil, timestamp: Integer|nil }`
  #
  # @example Sinatra
  #   post "/webhooks/epostak" do
  #     raw = request.body.read
  #     result = EPostak.verify_webhook_signature(
  #       payload: raw,
  #       signature_header: request.env["HTTP_X_EPOSTAK_SIGNATURE"].to_s,
  #       secret: ENV["EPOSTAK_WEBHOOK_SECRET"]
  #     )
  #     halt 400, "bad signature: #{result[:reason]}" unless result[:valid]
  #     event = JSON.parse(raw)
  #     # process event...
  #     status 204
  #   end
  def self.verify_webhook_signature(payload:, signature_header:, secret:, tolerance_seconds: 300)
    if signature_header.nil? || signature_header.empty?
      return { valid: false, reason: :missing_header, timestamp: nil }
    end

    timestamp_str = nil
    v1_signatures = []
    signature_header.split(",").each do |part|
      stripped = part.strip
      eq = stripped.index("=")
      next if eq.nil?

      key = stripped[0...eq]
      val = stripped[(eq + 1)..]
      case key
      when "t"
        timestamp_str = val
      when "v1"
        v1_signatures << val
      end
    end

    return { valid: false, reason: :malformed_header, timestamp: nil } if timestamp_str.nil?
    return { valid: false, reason: :no_v1_signature, timestamp: nil } if v1_signatures.empty?

    timestamp = begin
      Integer(timestamp_str, 10)
    rescue ArgumentError, TypeError
      nil
    end
    return { valid: false, reason: :malformed_header, timestamp: nil } if timestamp.nil?

    if tolerance_seconds.positive?
      now = Time.now.to_i
      if (now - timestamp).abs > tolerance_seconds
        return { valid: false, reason: :timestamp_outside_tolerance, timestamp: timestamp }
      end
    end

    signed = "#{timestamp_str}.#{payload}"
    expected = OpenSSL::HMAC.hexdigest("SHA256", secret, signed)

    v1_signatures.each do |candidate|
      next if candidate.length != expected.length

      if secure_compare(candidate, expected)
        return { valid: true, reason: nil, timestamp: timestamp }
      end
    end

    { valid: false, reason: :signature_mismatch, timestamp: timestamp }
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
