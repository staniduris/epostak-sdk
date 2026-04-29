# frozen_string_literal: true

require_relative "lib/epostak/version"

Gem::Specification.new do |spec|
  spec.name          = "epostak"
  spec.version       = EPostak::VERSION
  spec.authors       = ["ePošťák"]
  spec.email         = ["info@epostak.sk"]

  spec.summary       = "Ruby SDK for the ePošťák API"
  spec.description   = "Send and receive e-invoices via the Slovak Peppol network. " \
                        "Wraps the ePošťák REST API with idiomatic Ruby methods " \
                        "for documents, inbox, firms, webhooks, Peppol lookup, audit, " \
                        "OAuth token flow, and AI extraction."
  spec.homepage      = "https://epostak.sk"
  spec.license       = "MIT"

  spec.required_ruby_version = ">= 3.0"

  spec.files         = Dir["lib/**/*.rb"] + ["epostak.gemspec", "Gemfile", "README.md", "CHANGELOG.md"]
  spec.require_paths = ["lib"]

  spec.add_dependency "faraday",           "~> 2.0"
  spec.add_dependency "faraday-multipart", "~> 1.0"
end
