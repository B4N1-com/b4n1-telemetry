# frozen_string_literal: true

require "json"
require "net/http"
require "uri"

module B4N1Telemetry
  # The POST itself: the only place that knows HTTP exists, and the only place
  # that retries.
  #
  # A missing answer is not a refusal. A timeout against a healthy collector
  # means the collector is fsyncing, and dropping the batch there is silent
  # loss, so the request is attempted once more. A 4xx is a bug in the payload
  # and retrying repeats it; a 429 is the collector asking for time, which the
  # client answers with its requeue path.
  class Transport
    RETRY_PAUSE = 0.1

    # status is nil when no answer arrived at all; error says why.
    Result = Struct.new(:status, :retry_after, :error, keyword_init: true)

    def initialize(url, token, timeout)
      @url = url
      @token = token
      @timeout = timeout
    end

    def post(batch)
      attempt = 0
      begin
        attempt += 1
        response = deliver(batch)
        Result.new(status: response.code.to_i,
                   retry_after: response["Retry-After"],
                   error: nil)
      rescue StandardError => e
        if attempt == 1
          sleep RETRY_PAUSE
          retry
        end
        Result.new(status: nil, retry_after: nil, error: "#{e.class}: #{e.message}")
      end
    end

    private

    def deliver(batch)
      uri = URI.parse("#{@url}/api/v1/events")
      request = Net::HTTP::Post.new(uri)
      request["Content-Type"] = "application/json"
      request["Authorization"] = "Bearer #{@token}" if @token
      request.body = JSON.generate(batch)
      Net::HTTP.start(uri.host, uri.port, use_ssl: uri.scheme == "https",
                                            open_timeout: @timeout,
                                            read_timeout: @timeout) do |http|
        http.request(request)
      end
    end
  end
end
