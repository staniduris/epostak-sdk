# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # SAPI-SK 1.0 interoperable document send/receive endpoints.
    class Sapi
      def initialize(http, base_url:)
        @http = http
        @base_url = base_url
        @participants = SapiParticipants.new(self)
      end

      attr_reader :participants

      def send_document(body, participant_id:, idempotency_key:)
        sapi_request(
          :post,
          "/sapi/v1/document/send",
          body: body,
          participant_id: participant_id,
          idempotency_key: idempotency_key
        )
      end

      def receive(participant_id:, limit: nil, status: nil, page_token: nil)
        query = compact(limit: limit, status: status, pageToken: page_token)
        sapi_request(
          :get,
          "/sapi/v1/document/receive#{query_string(query)}",
          participant_id: participant_id
        )
      end

      def get(document_id, participant_id:)
        sapi_request(
          :get,
          "/sapi/v1/document/receive/#{encode(document_id)}",
          participant_id: participant_id
        )
      end

      def acknowledge(document_id, participant_id:)
        sapi_request(
          :post,
          "/sapi/v1/document/receive/#{encode(document_id)}/acknowledge",
          participant_id: participant_id
        )
      end

      private

      def sapi_request(method, path, participant_id:, body: nil, idempotency_key: nil)
        @http.request(
          method,
          path,
          body: body,
          idempotency_key: idempotency_key,
          query: nil,
          retry_on_failure: method == :get,
          headers: { "X-Peppol-Participant-Id" => participant_id },
          base_url: @base_url.sub(%r{/api/v1/?\z}, "")
        )
      end

      def compact(hash)
        hash.reject { |_k, v| v.nil? }
      end

      def query_string(hash)
        return "" if hash.empty?

        "?" + hash.map { |k, v| "#{encode(k)}=#{encode(v)}" }.join("&")
      end

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end

    class SapiParticipants
      def initialize(sapi)
        @sapi = sapi
      end

      def for_participant(participant_id)
        SapiParticipant.new(@sapi, participant_id)
      end
    end

    class SapiParticipant
      attr_reader :documents

      def initialize(sapi, participant_id)
        @documents = SapiParticipantDocuments.new(sapi, participant_id)
      end
    end

    class SapiParticipantDocuments
      def initialize(sapi, participant_id)
        @sapi = sapi
        @participant_id = participant_id
      end

      def send_document(body, idempotency_key:)
        @sapi.send_document(body, participant_id: @participant_id, idempotency_key: idempotency_key)
      end

      def send(body, idempotency_key:)
        send_document(body, idempotency_key: idempotency_key)
      end

      def receive(limit: nil, status: nil, page_token: nil)
        @sapi.receive(participant_id: @participant_id, limit: limit, status: status, page_token: page_token)
      end

      def get(document_id)
        @sapi.get(document_id, participant_id: @participant_id)
      end

      def acknowledge(document_id)
        @sapi.acknowledge(document_id, participant_id: @participant_id)
      end
    end
  end
end
