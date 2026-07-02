# frozen_string_literal: true

require "stringio"

module EPostak
  module Resources
    # Payload Assistant helpers for creating send payloads.
    class Payloads
      def initialize(http)
        @http = http
      end

      def extract(file_path_or_io, mime_type, file_name: "document")
        @http.request_multipart("/payloads/extract", [
          { field: "file", io: resolve_io(file_path_or_io), mime_type: mime_type, filename: file_name }
        ])
      end

      def extract_batch(files)
        parts = files.map do |f|
          {
            field: "files",
            io: resolve_io(f[:file]),
            mime_type: f[:mime_type],
            filename: f[:file_name] || "document"
          }
        end
        @http.request_multipart("/payloads/extract/batch", parts)
      end

      def parse(xml)
        @http.request(:post, "/payloads/parse", body: { xml: xml })
      end

      def convert(input_format:, output_format:, document:)
        @http.request(
          :post,
          "/payloads/convert",
          body: { input_format: input_format, output_format: output_format, document: document }
        )
      end

      def validate(body)
        @http.request(:post, "/payloads/validate", body: body)
      end

      private

      def resolve_io(file_path_or_io)
        file_path_or_io.is_a?(String) ? File.open(file_path_or_io, "rb") : file_path_or_io
      end
    end
  end
end
