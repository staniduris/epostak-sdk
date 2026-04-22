# frozen_string_literal: true

module EPostak
  module Resources
    # Resource for sending, receiving, and managing Peppol e-invoicing documents.
    #
    # This is the primary resource for document operations -- sending invoices,
    # checking delivery status, downloading PDFs, and responding to received invoices.
    #
    # @example Send an invoice
    #   result = client.documents.send_document(
    #     receiverPeppolId: "0245:1234567890",
    #     items: [{ description: "Web development", quantity: 40, unitPrice: 80, vatRate: 23 }]
    #   )
    #   status = client.documents.status(result["documentId"])
    class Documents
      # @return [Resources::Inbox] Sub-resource for managing received (inbound) documents
      attr_reader :inbox

      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http  = http
        @inbox = Inbox.new(http)
      end

      # Retrieve a single document by ID.
      #
      # @param id [String] Document UUID
      # @return [Hash] Full document object including parties, lines, and totals
      #
      # @example
      #   doc = client.documents.get("doc-uuid")
      #   puts doc["totals"]["withVat"] # => 1230.0
      def get(id)
        @http.request(:get, "/documents/#{encode(id)}")
      end

      # Update a draft document. Only documents that have not been sent yet
      # can be updated. Pass +nil+ to clear optional fields, or omit them
      # to leave unchanged.
      #
      # @param id [String] Document UUID of the draft to update
      # @param params [Hash] Fields to update (e.g. dueDate, note)
      # @return [Hash] The updated document
      #
      # @example
      #   updated = client.documents.update("doc-uuid", dueDate: "2026-05-15", note: "Updated terms")
      def update(id, **params)
        @http.request(:patch, "/documents/#{encode(id)}", body: params)
      end

      # Send an invoice via the Peppol network.
      #
      # Accepts either structured JSON (the API generates UBL XML) or
      # pre-built UBL XML via the +xml+ key.
      #
      # Note: named +send_document+ because +send+ is a reserved method in Ruby.
      #
      # @param body [Hash] Invoice data as JSON fields or raw UBL XML
      # @return [Hash] Document ID, Peppol message ID, and status confirmation
      #
      # @example Send with JSON (API generates UBL)
      #   result = client.documents.send_document(
      #     receiverPeppolId: "0245:1234567890",
      #     invoiceNumber: "FV-2026-042",
      #     items: [
      #       { description: "Consulting", quantity: 10, unit: "HUR", unitPrice: 100, vatRate: 23 }
      #     ]
      #   )
      #
      # @example Send with raw UBL XML
      #   result = client.documents.send_document(
      #     receiverPeppolId: "0245:1234567890",
      #     xml: '<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">...</Invoice>'
      #   )
      #
      # @example Send with attachments (BG-24, JSON mode only)
      #   Embedded into the generated UBL as base64. Allowed MIME: application/pdf,
      #   image/png, image/jpeg, text/csv, xlsx, ods. Limits: max 20 files,
      #   10 MB per file, 15 MB total.
      #
      #   result = client.documents.send_document(
      #     receiverPeppolId: "0245:1234567890",
      #     items: [{ description: "Consulting", quantity: 10, unitPrice: 100, vatRate: 23 }],
      #     attachments: [
      #       {
      #         fileName: "invoice-detail.pdf",
      #         mimeType: "application/pdf",
      #         content: Base64.strict_encode64(File.binread("invoice-detail.pdf")),
      #         description: "Timesheet breakdown"
      #       }
      #     ]
      #   )
      def send_document(body)
        @http.request(:post, "/documents/send", body: body)
      end

      # Get the current delivery status and full status history of a document.
      #
      # @param id [String] Document UUID
      # @return [Hash] Status details including history timeline and delivery timestamps
      #
      # @example
      #   status = client.documents.status("doc-uuid")
      #   puts status["status"]      # => "DELIVERED"
      #   puts status["deliveredAt"] # => "2026-04-11T12:30:00Z"
      def status(id)
        @http.request(:get, "/documents/#{encode(id)}/status")
      end

      # Retrieve delivery evidence for a sent document, including AS4 receipts,
      # Message Level Response (MLR), and Invoice Response from the buyer.
      #
      # @param id [String] Document UUID
      # @return [Hash] Evidence records (AS4 receipt, MLR, Invoice Response)
      #
      # @example
      #   evidence = client.documents.evidence("doc-uuid")
      #   if evidence.dig("invoiceResponse", "status") == "AP"
      #     puts "Invoice was accepted by the buyer"
      #   end
      def evidence(id)
        @http.request(:get, "/documents/#{encode(id)}/evidence")
      end

      # Download the PDF visualization of a document.
      #
      # @param id [String] Document UUID
      # @return [String] Raw PDF bytes as a binary string
      #
      # @example
      #   pdf_bytes = client.documents.pdf("doc-uuid")
      #   File.binwrite("invoice.pdf", pdf_bytes)
      def pdf(id)
        @http.request_raw(:get, "/documents/#{encode(id)}/pdf")
      end

      # Download the UBL XML of a document.
      #
      # @param id [String] Document UUID
      # @return [String] UBL 2.1 XML content as a string
      #
      # @example
      #   xml = client.documents.ubl("doc-uuid")
      #   puts xml # => '<?xml version="1.0"?><Invoice ...>...</Invoice>'
      def ubl(id)
        @http.request_raw(:get, "/documents/#{encode(id)}/ubl")
      end

      # Send an Invoice Response (accept, reject, or query) for a received document.
      # This sends a Peppol Invoice Response message back to the supplier.
      #
      # @param id [String] Document UUID of the received invoice
      # @param status [String] Response status code ("AP" = accepted, "RE" = rejected, "UQ" = under query)
      # @param note [String, nil] Optional note explaining the response
      # @return [Hash] Confirmation with the response status and timestamp
      #
      # @example Accept an invoice
      #   client.documents.respond("doc-uuid", status: "AP")
      #
      # @example Reject with a reason
      #   client.documents.respond("doc-uuid", status: "RE", note: "Incorrect VAT rate applied")
      def respond(id, status:, note: nil)
        body = { status: status }
        body[:note] = note if note
        @http.request(:post, "/documents/#{encode(id)}/respond", body: body)
      end

      # Validate a document without sending it. Checks Peppol BIS 3.0 compliance
      # and returns warnings. For JSON input, also returns the generated UBL XML preview.
      #
      # @param body [Hash] Document data to validate (same format as +send_document+)
      # @return [Hash] Validation result with +valid+ boolean, +warnings+ array, and optional UBL preview
      #
      # @example
      #   result = client.documents.validate(
      #     receiverPeppolId: "0245:1234567890",
      #     items: [{ description: "Test", quantity: 1, unitPrice: 100, vatRate: 23 }]
      #   )
      #   puts "Invalid!" unless result["valid"]
      def validate(body)
        @http.request(:post, "/documents/validate", body: body)
      end

      # Check if a Peppol receiver is registered and supports the target document
      # type before sending. Use this to avoid sending to non-existent participants.
      #
      # @param receiver_peppol_id [String] Peppol ID of the receiver (e.g. "0245:1234567890")
      # @param document_type_id [String, nil] Optional document type to check support for
      # @return [Hash] Preflight result with registration and capability info
      #
      # @example
      #   check = client.documents.preflight(receiver_peppol_id: "0245:1234567890")
      #   puts "Not on Peppol!" unless check["registered"]
      def preflight(receiver_peppol_id:, document_type_id: nil)
        body = { receiverPeppolId: receiver_peppol_id }
        body[:documentTypeId] = document_type_id if document_type_id
        @http.request(:post, "/documents/preflight", body: body)
      end

      # Convert between JSON and UBL XML formats without sending.
      # Useful for previewing the UBL output or parsing received XML into structured data.
      #
      # @param input_format [String] Format of the input document: "json" or "ubl"
      # @param output_format [String] Desired output format: "ubl" or "json"
      # @param document [Hash, String] The document to convert — a Hash for JSON input, a UBL XML string for UBL input
      # @return [Hash] Conversion result with "output_format", "document", and "warnings" keys
      #
      # @example JSON to UBL
      #   result = client.documents.convert(
      #     input_format: "json",
      #     output_format: "ubl",
      #     document: { invoiceNumber: "FV-001", items: [...] }
      #   )
      #   puts result["document"] # => UBL XML string
      #
      # @example UBL to JSON
      #   result = client.documents.convert(
      #     input_format: "ubl",
      #     output_format: "json",
      #     document: "<Invoice>...</Invoice>"
      #   )
      #   puts result["document"] # => Parsed invoice hash
      def convert(input_format:, output_format:, document:)
        body = {
          input_format: input_format,
          output_format: output_format,
          document: document
        }
        @http.request(:post, "/documents/convert", body: body)
      end

      # Send up to 100 documents in a single request.
      #
      # Each item uses the same body format as {#send_document} and may carry
      # an optional +idempotencyKey+ for safe retries. Partial failures do not
      # fail the whole request -- inspect +results+ and +failed+ per item.
      #
      # @param items [Array<Hash>] Array of send request bodies
      # @return [Hash] {"total" => ..., "succeeded" => ..., "failed" => ...,
      #   "results" => [{"index" => ..., "status" => ..., "result" => ...}]}
      #
      # @example
      #   batch = client.documents.send_batch([
      #     {
      #       receiverPeppolId: "0245:1234567890",
      #       invoiceNumber: "FV-2026-010",
      #       items: [{ description: "Audit", quantity: 1, unitPrice: 500, vatRate: 23 }],
      #       idempotencyKey: "batch-2026-04-22-001"
      #     },
      #     {
      #       receiverPeppolId: "0245:0987654321",
      #       invoiceNumber: "FV-2026-011",
      #       items: [{ description: "Consulting", quantity: 2, unitPrice: 300, vatRate: 23 }]
      #     }
      #   ])
      #   puts "#{batch['succeeded']} / #{batch['total']}"
      def send_batch(items)
        @http.request(:post, "/documents/send/batch", body: { items: items })
      end

      # Parse a UBL XML invoice into a structured JSON representation.
      #
      # Streams the XML as the raw request body with
      # +Content-Type: application/xml+.
      #
      # @param xml [String] UBL 2.1 XML invoice or credit note as a string
      # @return [Hash] Parsed invoice hash with supplier, customer, lines, totals, etc.
      #
      # @example
      #   parsed = client.documents.parse(File.read("invoice.xml"))
      #   puts parsed["invoiceNumber"]
      def parse(xml)
        @http.request_with_body(:post, "/documents/parse", xml, content_type: "application/xml")
      end

      # Manually mark a document's lifecycle state.
      #
      # Use for documents delivered out-of-band (e.g. a receiver confirms
      # over email) or to flag a failed/processed document in your own
      # workflow.
      #
      # @param id [String] Document UUID
      # @param state [String] One of "delivered", "processed", "failed", "read"
      # @param note [String, nil] Optional note attached to the state change
      # @return [Hash] {"id" => ..., "state" => ..., "status" => ...,
      #   "deliveredAt" => ..., "acknowledgedAt" => ..., "readAt" => ...}
      #
      # @example
      #   result = client.documents.mark("doc-uuid", state: "delivered", note: "Confirmed by email")
      #   puts result["deliveredAt"]
      def mark(id, state:, note: nil)
        body = { state: state }
        body[:note] = note if note
        @http.request(:post, "/documents/#{encode(id)}/mark", body: body)
      end

      private

      # URI-encode a path segment.
      # @param value [String]
      # @return [String]
      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end

require "erb"
