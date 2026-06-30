# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Resource for Peppol network operations -- SMP lookups, directory search,
    # and Slovak company information retrieval.
    #
    # @example
    #   participant = client.peppol.lookup("0245", "1234567890")
    #   puts participant["routingStatus"]
    class Peppol
      # @return [Resources::PeppolDirectory] Sub-resource for searching the Peppol Business Card directory
      attr_reader :directory

      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http      = http
        @directory = PeppolDirectory.new(http)
      end

      # Look up a Peppol participant by their identifier scheme and value.
      # Queries the SMP (Service Metadata Publisher) to check participant
      # existence and the default BIS Billing invoice capability.
      #
      # Slovak Peppol ID format: scheme "0245" with identifier = DIC value.
      #
      # @param scheme [String] Peppol identifier scheme (e.g. "0245")
      # @param identifier [String] Identifier value (e.g. "1234567890")
      # @return [Hash] Participant invoice capability. Use
      #   found && accepts && routingStatus == "ready" before send.
      #
      # @example
      #   participant = client.peppol.lookup("0245", "1234567890")
      #   if participant["found"] && participant["accepts"]
      #     puts participant["accessPoint"]&.fetch("url", nil)
      #   end
      def lookup(scheme, identifier)
        @http.request(:get, "/peppol/participants/#{encode(scheme)}/#{encode(identifier)}")
      end

      # Look up a Slovak company by its ICO (business registration number).
      # Queries Slovak business registers (ORSR, FinStat) and checks Peppol
      # registration status. Returns company name, tax IDs, address, and Peppol ID.
      #
      # @param ico [String] Slovak ICO (8-digit business registration number)
      # @return [Hash] Company information including Peppol registration status
      #
      # @example
      #   company = client.peppol.company_lookup("12345678")
      #   puts company["name"]     # => "ACME s.r.o."
      #   puts company["peppolId"] # => "0245:1234567890" or nil
      def company_lookup(ico)
        @http.request(:get, "/company/lookup/#{encode(ico)}")
      end

      def company_search(q:, limit: nil)
        @http.request(:get, "/company/search", query: { q: q, limit: limit })
      end

      def resolve(**params)
        query = {
          ico: params[:ico],
          dic: params[:dic],
          icDph: params[:ic_dph] || params[:icDph],
          peppolId: params[:peppol_id] || params[:peppolId],
          scheme: params[:scheme],
          identifier: params[:identifier],
          documentTypeId: params[:document_type_id] || params[:documentTypeId],
          processId: params[:process_id] || params[:processId]
        }.compact
        @http.request(:get, "/peppol/participants/resolve", query: query)
      end

      # Check a participant's advertised Peppol capabilities.
      #
      # Verifies that a participant exists on SMP and (optionally) that
      # it accepts a specific document type. Prefer this over {#lookup}
      # when you only need a yes/no answer for a given doc type.
      #
      # @param scheme [String] Peppol scheme (e.g. "0245")
      # @param identifier [String] Identifier value
      # @param document_type [String, nil] Optional UBL document type ID
      # @return [Hash] {"found" => ..., "accepts" => ...,
      #   "supportedDocumentTypes" => [...], "matchedDocumentType" => ...}
      #
      # @example
      #   caps = client.peppol.capabilities(
      #     scheme: "0245",
      #     identifier: "12345678",
      #     document_type: "urn:peppol:pint:billing-1@aunz-1"
      #   )
      #   puts "Supported" if caps["found"] && caps["accepts"]
      def capabilities(scheme:, identifier:, document_type: nil)
        body = { participant: { scheme: scheme, identifier: identifier } }
        body[:documentType] = document_type if document_type
        @http.request(:post, "/peppol/capabilities", body: body)
      end

      # Look up many Peppol participants in a single request (max 100).
      #
      # Each result matches the order of the input list and separates
      # participant existence from invoice routing capability.
      #
      # @param participants [Array<Hash>] Array of { scheme:, identifier: } hashes
      # @return [Hash] {"total" => ..., "found" => ..., "notFound" => ...,
      #   "results" => [{"index" => ..., "participant" => ..., "found" => ...,
      #   "accepts" => ..., "routingStatus" => ..., "error" => ...}]}
      #
      # @example
      #   batch = client.peppol.lookup_batch([
      #     { scheme: "0245", identifier: "12345678" },
      #     { scheme: "0245", identifier: "87654321" }
      #   ])
      #   batch["results"].each { |r| puts "#{r['participant']['id']} -> #{r['routingStatus']}" }
      def lookup_batch(participants)
        @http.request(:post, "/peppol/participants/batch", body: { participants: participants })
      end

      private

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
