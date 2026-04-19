# frozen_string_literal: true

Gem::Specification.new do |spec|
  spec.name          = "epostak"
  spec.version       = "1.0.1"
  spec.authors       = ["ePosťák"]
  spec.email         = ["info@epostak.sk"]

  spec.summary       = "Ruby SDK for the ePosťák Enterprise API"
  spec.description   = "Send and receive e-invoices via the Slovak Peppol network. " \
                        "Wraps the ePosťák Enterprise REST API with idiomatic Ruby methods " \
                        "for documents, inbox, firms, webhooks, Peppol lookup, and AI extraction."
  spec.homepage      = "https://epostak.sk"
  spec.license       = "MIT"

  spec.required_ruby_version = ">= 3.0"

  spec.files         = Dir["lib/**/*.rb"] + ["epostak.gemspec", "Gemfile", "README.md"]
  spec.require_paths = ["lib"]

  spec.add_dependency "faraday",           "~> 2.0"
  spec.add_dependency "faraday-multipart", "~> 1.0"
end
