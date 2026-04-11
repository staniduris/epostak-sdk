# frozen_string_literal: true

require_relative "epostak/error"
require_relative "epostak/http_client"
require_relative "epostak/client"
require_relative "epostak/resources/documents"
require_relative "epostak/resources/inbox"
require_relative "epostak/resources/firms"
require_relative "epostak/resources/peppol"
require_relative "epostak/resources/peppol_directory"
require_relative "epostak/resources/webhooks"
require_relative "epostak/resources/webhook_queue"
require_relative "epostak/resources/reporting"
require_relative "epostak/resources/account"
require_relative "epostak/resources/extract"

# Top-level namespace for the ePosťák Enterprise API SDK.
#
# @example Quick start
#   client = EPostak::Client.new(api_key: "sk_live_xxxxx")
#   result = client.documents.send_document(
#     receiverPeppolId: "0245:1234567890",
#     items: [{ description: "Consulting", quantity: 10, unitPrice: 100, vatRate: 23 }]
#   )
module EPostak
  # Default base URL for the ePosťák Enterprise API
  DEFAULT_BASE_URL = "https://epostak.sk/api/enterprise"
end
