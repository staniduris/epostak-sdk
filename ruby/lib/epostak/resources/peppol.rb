# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Resource for Peppol network operations -- SMP lookups, directory search,
    # and Slovak company information retrieval.
    #
    # @example
    #   participant = client.peppol.lookup("0245", "1234567890")
    #   puts participant["capabilities"]
    class Peppol
      # @return [Resources::PeppolDirectory] Sub-resource for searching the Peppol Business Card directory
      attr_reader :directory

      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http      = http
        @directory = PeppolDirectory.new(http)
      end

      # Look up a Peppol participant by their identifier scheme and value.
      # Queries the SMP (Service Metadata Publisher) to retrieve the participant's
      # name, country, and supported document types.
      #
      # Slovak Peppol ID format: scheme "0245" with identifier = DIC value,
      # or scheme "9950" with identifier = "SK" + DIC.
      #
      # @param scheme [String] Peppol identifier scheme (e.g. "0245")
      # @param identifier [String] Identifier value (e.g. "1234567890")
      # @return [Hash] Participant details including capabilities (supported document types)
      #
      # @example
      #   participant = client.peppol.lookup("0245", "1234567890")
      #   if participant["capabilities"].any?
      #     puts participant["capabilities"].map { |c| c["documentTypeId"] }
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

      private

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
