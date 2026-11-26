# frozen_string_literal: true

require_relative "lib/b4n1_telemetry/version"

Gem::Specification.new do |spec|
  spec.name = "b4n1-telemetry"
  spec.version = B4N1Telemetry::VERSION
  spec.summary = "Drop-in error reporting to your own b4n1-telemetry collector."
  spec.description = "Uncaught exceptions, explicit captures, batching and correlation " \
                     "for a self-hosted b4n1-telemetry collector. Standard library only."
  spec.authors = ["B4N1"]
  spec.license = "BUSL-1.1"
  spec.required_ruby_version = ">= 2.7"
  spec.files = Dir["lib/**/*.rb", "README.md", "LICENSE", "EULA.txt"]
  spec.require_paths = ["lib"]
  spec.metadata = { "rubygems_mfa_required" => "true" }
end
