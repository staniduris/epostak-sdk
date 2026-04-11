# frozen_string_literal: true

require "stringio"

module EPostak
  module Resources
    # Resource for AI-powered data extraction from PDF invoices and scanned images.
    # Extracts structured invoice data (parties, line items, totals) and generates
    # UBL 2.1 XML that can be sent via Peppol.
    #
    # @example
    #   result = client.extract.single(File.open("invoice.pdf", "rb"), "application/pdf", file_name: "invoice.pdf")
    #   puts result["confidence"] # => 0.95
    #   puts result["ubl_xml"]   # => Generated UBL XML
    class Extract
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # Extract structured data from a single PDF or image file.
      # Uses AI-powered OCR to identify invoice fields, line items, parties,
      # and totals, then generates UBL 2.1 XML from the extracted data.
      #
      # @param file_path_or_io [String, IO, StringIO] File path string, or an IO-like object with read support
      # @param mime_type [String] MIME type of the file (e.g. "application/pdf", "image/png")
      # @param file_name [String] Original file name for reference (default: "document")
      # @return [Hash] Extracted data with "confidence" score and "ubl_xml" string
      #
      # @example From a file path
      #   result = client.extract.single("invoice.pdf", "application/pdf")
      #   if result["confidence"] > 0.8
      #     client.documents.send_document(receiverPeppolId: "0245:1234567890", xml: result["ubl_xml"])
      #   end
      #
      # @example From an IO object
      #   io = File.open("scan.png", "rb")
      #   result = client.extract.single(io, "image/png", file_name: "scan.png")
      def single(file_path_or_io, mime_type, file_name: "document")
        io = resolve_io(file_path_or_io)
        @http.request_multipart("/extract", [
          { field: "file", io: io, mime_type: mime_type, filename: file_name }
        ])
      end

      # Extract structured data from multiple PDF or image files in a single request.
      # Each file is processed independently -- individual failures don't affect others.
      #
      # @param files [Array<Hash>] Array of file descriptors, each with:
      #   - +:file+ [String, IO] - File path or IO object
      #   - +:mime_type+ [String] - MIME type
      #   - +:file_name+ [String] - Optional file name (default: "document")
      # @return [Hash] Batch result with "results" array, "successful" and "total" counts
      #
      # @example
      #   result = client.extract.batch([
      #     { file: "invoice1.pdf", mime_type: "application/pdf", file_name: "invoice1.pdf" },
      #     { file: "invoice2.pdf", mime_type: "application/pdf", file_name: "invoice2.pdf" }
      #   ])
      #   puts "#{result['successful']}/#{result['total']} extracted successfully"
      def batch(files)
        parts = files.map do |f|
          {
            field: "files",
            io: resolve_io(f[:file]),
            mime_type: f[:mime_type],
            filename: f[:file_name] || "document"
          }
        end
        @http.request_multipart("/extract/batch", parts)
      end

      private

      # Convert a file path string to an IO object, or return an IO as-is.
      #
      # @param file_path_or_io [String, IO]
      # @return [IO]
      def resolve_io(file_path_or_io)
        if file_path_or_io.is_a?(String)
          File.open(file_path_or_io, "rb")
        else
          file_path_or_io
        end
      end
    end
  end
end
