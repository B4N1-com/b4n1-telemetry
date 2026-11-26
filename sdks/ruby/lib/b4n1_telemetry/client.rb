# frozen_string_literal: true

require "json"
require "net/http"
require "uri"
require_relative "envelope"
require_relative "transport"

module B4N1Telemetry
  # Bounded queue drained by a background sender.
  #
  # Sending is best-effort by design: an observability client that raises or
  # blocks inside the application turns a bug report into an outage, so every
  # failure lands in #stats instead. The one failure that is not merely counted
  # is a 429: the batch goes back to the head of the queue and sending pauses
  # for Retry-After, because retrying at once would earn another 429 and
  # dropping it would make the collector's quota look like lost telemetry.
  class Client
    attr_reader :url, :app, :release

    def initialize(url: nil, token: nil, app: nil, release: nil, queue_size: 2048,
                   batch_size: 100, flush_interval: 0.25, timeout: 3.0, on_error: nil)
      @url = (url || ENV["B4N1_TELEMETRY_URL"] || Envelope::DEFAULT_URL).sub(%r{/+\z}, "")
      @token = token || ENV["B4N1_TELEMETRY_TOKEN"]
      @app = app || ENV.fetch("B4N1_TELEMETRY_APP", "ruby")
      @release = release || ENV["B4N1_TELEMETRY_RELEASE"]
      @queue_size = queue_size
      @batch_size = [batch_size, 1].max
      @flush_interval = [flush_interval, 0.01].max
      @timeout = timeout
      @on_error = on_error

      @queue = []
      @mutex = Mutex.new
      @sent = 0
      @failed = 0
      @dropped = 0
      @throttled = 0
      @inflight = 0
      @last_error = nil
      @paused_until = 0.0
      @closed = false
      @transport = Transport.new(@url, @token, @timeout)
      @sender = Thread.new { sender_loop }
      @sender.abort_on_exception = false
    end

    # Enqueue one event. Returns false when the queue is full (counted).
    def send_event(level: "error", message: "", component: "", data: nil,
                   backtrace: nil, correlation_id: nil, breadcrumbs: nil)
      enqueue(Envelope.build(app: @app, level: level, message: message, component: component,
                             data: data, backtrace: backtrace,
                             correlation_id: correlation_id,
                             breadcrumbs: breadcrumbs, release: @release))
    end

    # Report an exception at level "error", with its real backtrace.
    def capture_exception(error, component: "")
      return false if error.nil?

      enqueue(Envelope.build(app: @app, level: "error",
                             message: Envelope.describe(error), component: component,
                             data: { "error_type" => error.class.to_s },
                             backtrace: Envelope.backtrace_lines(error),
                             release: @release))
    end

    def enqueue(envelope)
      should_flush = false
      @mutex.synchronize do
        return false if @closed
        if @queue.length >= @queue_size
          @dropped += 1
          return false
        end
        @queue << envelope
        should_flush = @queue.length >= @batch_size
      end
      flush(2.0) if should_flush
      true
    end

    # Resolves true when everything queued has been sent (or dropped) within
    # `timeout`. An event already in flight counts as pending: reporting
    # "flushed" while the POST is open is how a caller loses data.
    def flush(timeout = 2.0)
      deadline = monotonic + timeout
      loop do
        batch = take
        post(batch) if batch
        return true if pending.zero?

        remaining = deadline - monotonic
        return false if remaining <= 0

        pause = @paused_until - monotonic
        sleep([pause.positive? ? pause : 0.01, remaining].min)
      end
    end

    def shutdown(timeout = 2.0)
      flush(timeout)
      @mutex.synchronize { @closed = true }
      @sender.kill
      @sender.join(1)
    end

    def stats
      @mutex.synchronize do
        { url: @url, sent: @sent, failed: @failed, dropped: @dropped,
          throttled: @throttled, queued: @queue.length, inflight: @inflight,
          last_error: @last_error }
      end
    end

    private

    def monotonic
      Process.clock_gettime(Process::CLOCK_MONOTONIC)
    end

    def pending
      @mutex.synchronize { @queue.length + @inflight }
    end

    def sender_loop
      loop do
        sleep @flush_interval
        flush(2.0) unless @closed
      end
    rescue StandardError
      nil # the sender thread must never take the application down
    end

    def take
      @mutex.synchronize do
        return nil if @queue.empty? || @closed || monotonic < @paused_until

        batch = @queue.shift(@batch_size)
        @inflight += batch.length
        batch
      end
    end

    def post(batch)
      settle(batch, @transport.post(batch))
    end

    def settle(batch, result)
      status = result.status
      @mutex.synchronize do
        @inflight -= batch.length
        if result.error
          @failed += 1
          @last_error = result.error
        elsif status.between?(200, 299)
          @sent += 1
        elsif status == 429
          @throttled += 1
          seconds = (result.retry_after || "1").to_i
          seconds = 1 unless seconds.positive?
          @paused_until = monotonic + seconds
          @last_error = "HTTP 429 (retry in #{seconds}s)"
          requeue(batch)
        else
          @failed += 1
          @last_error = "HTTP #{status}"
        end
      end
      notify(@last_error) if result.error || !status.between?(200, 299)
    end

    # The refused batch is the oldest, so it goes back FIRST, then whatever
    # still fits. What does not fit is counted, never hidden.
    def requeue(batch)
      room = [@queue_size - batch.length, 0].max
      kept = @queue.slice!(0, room)
      @dropped += @queue.length
      @queue.clear
      @queue.concat(batch).concat(kept)
    end

    def notify(message)
      @on_error&.call(message)
    rescue StandardError
      nil # an error hook must never break the sender
    end
  end
end
