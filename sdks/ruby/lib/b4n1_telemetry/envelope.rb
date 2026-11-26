# frozen_string_literal: true

require "json"
require "time"

module B4N1Telemetry
  # Wire envelope, field for field the collector's TelemetryEvent.
  #
  # A half-populated event must never reach the wire, so every optional field
  # gets its defined zero value here (nil correlation_id, empty lists) instead
  # of being left out.
  module Envelope
    VERSION = 1
    DEFAULT_URL = "http://127.0.0.1:9450"
    LEVELS = %w[debug info warning error fatal].freeze

    module_function

    # RFC 3339 UTC with milliseconds — the form the collector parses.
    def now_iso
      Time.now.utc.strftime("%Y-%m-%dT%H:%M:%S.%LZ")
    end

    # `component` is the collector's `module` field. It is named differently
    # here because `module` is a Ruby keyword and cannot be a parameter name;
    # the wire keeps the collector's spelling so every language agrees.
    def build(app:, level: "error", message: "", component: "", data: nil,
              backtrace: nil, correlation_id: nil, breadcrumbs: nil,
              timestamp: nil, release: nil)
      level = level.to_s
      raise ArgumentError, "unknown level #{level.inspect}" unless LEVELS.include?(level)

      event = {
        "timestamp" => timestamp || now_iso,
        "app" => app.to_s,
        "level" => level,
        "message" => message.to_s,
        "module" => component.to_s,
        "data" => stringify(data),
        "backtrace" => Array(backtrace),
        "correlation_id" => correlation_id,
        "breadcrumbs" => Array(breadcrumbs)
      }
      # Omitted when unset so an event from an older producer stays valid.
      event["release"] = release unless release.nil? || release.to_s.empty?
      { "v" => VERSION, "event" => event }
    end

    # The wire carries strings only: a numeric 2 and a string "2" must group
    # together on the dashboard instead of splitting one issue in two.
    def stringify(data)
      (data || {}).each_with_object({}) do |(key, value), out|
        out[key.to_s] = value.to_s
      end
    end

    # `"RuntimeError: boom"` — the message every other SDK uses for the same
    # error, so one collector groups them together.
    def describe(error)
      "#{error.class}: #{error.message}"
    end

    # A Ruby backtrace as one string per frame, like a Python traceback.
    def backtrace_lines(error)
      Array(error.backtrace)
    end
  end
end
