# frozen_string_literal: true

require_relative "b4n1_telemetry/envelope"
require_relative "b4n1_telemetry/client"

# b4n1-telemetry for Ruby: drop-in error reporting to your own collector.
#
#   require "b4n1_telemetry"
#   B4N1Telemetry.init(app: "api")
#
# After that, `capture` and `capture_exception` report without any per-call-site
# configuration, and an `at_exit` hook flushes whatever is still queued so a
# process that ends normally does not lose its last events.
#
# Standard library only: the SDK adds no dependency to the application.
# Environment: B4N1_TELEMETRY_URL, B4N1_TELEMETRY_TOKEN, B4N1_TELEMETRY_APP,
# B4N1_TELEMETRY_RELEASE.
module B4N1Telemetry
  CORRELATION_KEY = :b4n1_correlation_id
  class << self
    attr_reader :client

    # Install the package-level client. Idempotent: a second call keeps the
    # first client instead of sending every event twice.
    def init(**options)
      return @client if @client

      @client = Client.new(**options)
      install_at_exit
      @client
    end

    def capture(message, level: "error", component: "", data: nil, backtrace: nil)
      return false unless @client

      @client.send_event(level: level, message: message, component: component, data: data,
                         backtrace: backtrace, correlation_id: correlation_id)
    end

    def capture_exception(error, component: "")
      return false unless @client

      @client.capture_exception(error, component: component)
    end

    def flush(timeout = 2.0)
      return true unless @client

      @client.flush(timeout)
    end

    def shutdown(timeout = 2.0)
      return unless @client

      @client.shutdown(timeout)
      @client = nil
    end

    # Correlation lives on the thread, which is the unit a web worker handles
    # one request in. A module-level variable would leak one request's id into
    # every concurrent request — worse than no correlation at all.
    def with_correlation(id)
      previous = Thread.current[CORRELATION_KEY]
      Thread.current[CORRELATION_KEY] = id
      yield
    ensure
      Thread.current[CORRELATION_KEY] = previous
    end

    def correlation_id
      Thread.current[CORRELATION_KEY]
    end

    private

    def install_at_exit
      return if @at_exit_installed

      @at_exit_installed = true
      at_exit { shutdown }
    end
  end
end
