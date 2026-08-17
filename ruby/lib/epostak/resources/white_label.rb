# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Integrator-scoped White Label participant registration and migration.
    class WhiteLabel
      def initialize(http)
        @http = http
      end

      def list_participants(limit: nil, cursor: nil)
        @http.request(
          :get,
          "/white-label/participants",
          query: { limit: limit, cursor: cursor }.compact,
          omit_firm_id: true
        )
      end

      # verificationToken is a one-time FS SR secret. Never log the request.
      def register_participant(request, idempotency_key:)
        post_idempotent("/white-label/participants/registrations", request, idempotency_key)
      end

      # migrationCode is an SMP secret. Never log the request.
      def migrate_participant(request, idempotency_key:)
        post_idempotent("/white-label/participants/migrations", request, idempotency_key)
      end

      def get_participant(participant_id)
        @http.request(
          :get,
          "/white-label/participants/#{ERB::Util.url_encode(participant_id)}",
          omit_firm_id: true
        )
      end

      def request_migration_code(participant_id, idempotency_key:)
        key = idempotency_key!(idempotency_key)
        @http.request(
          :post,
          "/white-label/participants/#{ERB::Util.url_encode(participant_id)}/migration-code",
          idempotency_key: key,
          retry_on_failure: true,
          retry_network_errors: true,
          omit_firm_id: true
        )
      end

      def get_operation(operation_id)
        @http.request(
          :get,
          "/white-label/operations/#{ERB::Util.url_encode(operation_id)}",
          omit_firm_id: true
        )
      end

      private

      def post_idempotent(path, request, idempotency_key)
        key = idempotency_key!(idempotency_key)
        @http.request(
          :post,
          path,
          body: request,
          idempotency_key: key,
          retry_on_failure: true,
          retry_network_errors: true,
          omit_firm_id: true
        )
      end

      def idempotency_key!(value)
        unless value.is_a?(String) && !value.strip.empty? && value.bytesize <= 255
          raise ArgumentError, "White Label idempotency key must be 1-255 UTF-8 bytes"
        end

        value
      end
    end
  end
end
