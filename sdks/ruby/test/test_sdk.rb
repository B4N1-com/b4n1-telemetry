# frozen_string_literal: true

# Offline suite: a real local HTTP server, real envelopes, no network.
#
# The double speaks the collector's protocol on a raw socket (no WEBrick, which
# is no longer a stdlib gem) and can answer 429 on demand, so the quota path is
# exercised the way production does.

require "json"
require "minitest/autorun"
require "socket"
require_relative "../lib/b4n1_telemetry"

class Collector
  attr_reader :batches, :auth

  def initialize(statuses = [])
    @server = TCPServer.new("127.0.0.1", 0)
    @statuses = statuses
    @batches = []
    @auth = []
    @thread = Thread.new do
      loop { handle(@server.accept) }
    rescue IOError, Errno::EBADF
      nil # closed by #stop
    end
  end

  def url
    "http://127.0.0.1:#{@server.addr[1]}"
  end

  def events
    @batches.flatten.map { |envelope| envelope["event"] }
  end

  def stop
    @server.close
    @thread.join(1)
  end

  private

  def handle(socket)
    request_line = socket.gets
    headers = {}
    while (line = socket.gets) && line != "\r\n"
      key, value = line.split(":", 2)
      headers[key.downcase] = value.to_s.strip
    end
    body = socket.read(headers["content-length"].to_i)
    status = @statuses.shift || 202
    # -1 means no answer at all: the transport failure the retry exists for.
    return if status == -1

    # A refused batch stores nothing: recording it would fake a duplicate that
    # production never creates.
    if status < 300
      @batches << JSON.parse(body)
      @auth << headers["authorization"]
    end
    reason = { 202 => "Accepted", 401 => "Unauthorized", 429 => "Too Many Requests" }[status]
    extra = status == 429 ? "Retry-After: 1\r\n" : ""
    socket.write("HTTP/1.1 #{status} #{reason}\r\n#{extra}Content-Length: 0\r\n" \
                 "Connection: close\r\n\r\n")
  ensure
    socket.close
  end
end

class SdkTest < Minitest::Test
  def setup
    @collector = Collector.new
    @client = B4N1Telemetry::Client.new(url: @collector.url, app: "api", token: "tkn",
                                        release: "sha-1", flush_interval: 10)
  end

  def teardown
    @client.shutdown(0.5)
    @collector.stop
  end

  def test_a_dropped_connection_is_retried_not_lost
    collector = Collector.new([-1])
    client = B4N1Telemetry::Client.new(url: collector.url, app: "retry", flush_interval: 0.01)
    client.send_event(level: "error", message: "kept")

    assert client.flush(5.0), "batch lost after a dropped connection"
    stats = client.stats
    assert_equal 0, stats[:failed], "a missing answer must be retried, not counted"
    assert_equal 1, stats[:sent]
    assert_equal ["kept"], collector.events.map { |event| event["message"] }
  ensure
    client&.shutdown(0.5)
    collector&.stop
  end

  def test_sends_a_v1_envelope_the_collector_parses
    @client.send_event(level: "error", message: "boom", component: "db", data: { attempt: 2 })
    assert @client.flush(2.0), "flush reported pending events"

    assert_equal 1, @collector.events.length
    event = @collector.events.first
    assert_equal "api", event["app"]
    assert_equal "error", event["level"]
    assert_equal "boom", event["message"]
    assert_equal "2", event["data"]["attempt"], "data must travel as strings"
    assert_equal "sha-1", event["release"]
    assert_match(/\A\d{4}-\d{2}-\d{2}T.*Z\z/, event["timestamp"])
    assert_equal [], event["backtrace"]
    assert_equal "Bearer tkn", @collector.auth.first
    assert_equal 1, @client.stats[:sent]
  end

  def test_events_are_batched_into_one_request
    @client = B4N1Telemetry::Client.new(url: @collector.url, app: "batch",
                                        batch_size: 50, flush_interval: 0.02)
    5.times { |i| @client.send_event(message: "m#{i}") }
    assert @client.flush(3.0)
    assert_equal 5, @collector.events.length
    assert_operator @collector.batches.length, :<=, 2, "five events must not mean five requests"
  end

  def test_a_429_pauses_the_sender_and_keeps_the_event
    @collector = Collector.new([429])
    @client = B4N1Telemetry::Client.new(url: @collector.url, app: "limited", flush_interval: 10)
    @client.send_event(message: "kept")
    assert @client.flush(5.0), "event lost after 429"
    stats = @client.stats
    assert_equal 1, stats[:throttled]
    assert_equal 1, stats[:sent], "the retry after the pause succeeded"
    assert_equal 0, stats[:dropped], "a quota is not a reason to lose data"
    assert_equal ["kept"], @collector.events.map { |event| event["message"] }
  end

  def test_a_full_queue_drops_and_counts
    client = B4N1Telemetry::Client.new(url: "http://127.0.0.1:9", queue_size: 3,
                                       flush_interval: 600)
    6.times { |i| client.send_event(message: "m#{i}") }
    stats = client.stats
    assert_equal 3, stats[:dropped]
    assert_equal 3, stats[:queued]
    client.shutdown(0.2)
  end

  def test_capture_exception_keeps_the_class_and_backtrace
    begin
      raise ArgumentError, "bad input"
    rescue ArgumentError => e
      @client.capture_exception(e, component: "checkout")
    end
    assert @client.flush(2.0)
    event = @collector.events.first
    assert_equal "ArgumentError: bad input", event["message"]
    assert_equal "ArgumentError", event["data"]["error_type"]
    assert_equal "checkout", event["module"]
    refute_empty event["backtrace"], "the real backtrace must travel"
  end

  def test_correlation_is_scoped_to_the_thread
    B4N1Telemetry.init(url: @collector.url, app: "web", flush_interval: 10)
    other = Thread.new { B4N1Telemetry.correlation_id }
    B4N1Telemetry.with_correlation("req-42") do
      B4N1Telemetry.capture("inside the request")
      assert_equal "req-42", B4N1Telemetry.correlation_id
    end
    assert_nil other.value, "one thread's id must not leak into another"
    assert_nil B4N1Telemetry.correlation_id, "the id must be restored"
    assert B4N1Telemetry.flush(2.0)
    assert_equal "req-42", @collector.events.first["correlation_id"]
  ensure
    B4N1Telemetry.shutdown(0.5)
  end
end
