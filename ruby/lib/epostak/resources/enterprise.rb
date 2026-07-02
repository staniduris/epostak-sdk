# frozen_string_literal: true

module EPostak
  module Resources
    class EnterprisePull
      attr_reader :inbound, :outbound

      def initialize(inbound:, outbound:)
        @inbound = inbound
        @outbound = outbound
      end
    end

    class Enterprise
      attr_reader :auth, :audit, :documents, :inbox, :firms, :peppol,
                  :webhooks, :reporting, :account, :extract, :integrator,
                  :connector, :payloads, :events, :box, :pull

      def initialize(client)
        @auth = client.auth
        @box = client.box
        @audit = client.audit
        @documents = client.documents
        @inbox = client.documents.inbox
        @firms = client.firms
        @peppol = client.peppol
        @webhooks = client.webhooks
        @reporting = client.reporting
        @account = client.account
        @extract = client.extract
        @integrator = client.integrator
        @connector = client.connector
        @payloads = client.payloads
        @events = client.events
        @pull = EnterprisePull.new(inbound: client.inbound, outbound: client.outbound)
      end
    end
  end
end
