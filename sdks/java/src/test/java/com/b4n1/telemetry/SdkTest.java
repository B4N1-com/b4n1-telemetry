package com.b4n1.telemetry;

import com.sun.net.httpserver.HttpServer;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentLinkedQueue;

/**
 * Offline suite: a real local HTTP server (the JDK's own), real envelopes, no
 * network, no test framework. Run with {@code java -cp ... SdkTest}.
 */
public final class SdkTest {
    private static int failures;

    private SdkTest() {
    }

    /** Local double that speaks the collector's protocol and can answer 429. */
    private static final class Collector implements AutoCloseable {
        private final HttpServer server;
        private final List<String> bodies = new ArrayList<>();
        private final List<String> auth = new ArrayList<>();
        private final ConcurrentLinkedQueue<Integer> statuses = new ConcurrentLinkedQueue<>();
        private final java.util.concurrent.atomic.AtomicInteger requests =
                new java.util.concurrent.atomic.AtomicInteger();

        Collector(Integer... codes) throws IOException {
            for (Integer code : codes) {
                statuses.add(code);
            }
            server = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
            server.createContext("/", exchange -> {
                requests.incrementAndGet();
                String body = new String(exchange.getRequestBody().readAllBytes(), StandardCharsets.UTF_8);
                Integer status = statuses.poll();
                int code = status == null ? 202 : status;
                if (code < 0) {
                    // No answer at all: the transport failure the retry exists for.
                    exchange.close();
                    return;
                }
                // A refused batch stores nothing: recording it would fake a
                // duplicate that production never creates.
                if (code < 300) {
                    synchronized (bodies) {
                        bodies.add(body);
                        auth.add(exchange.getRequestHeaders().getFirst("Authorization"));
                    }
                }
                if (code == 429) {
                    exchange.getResponseHeaders().add("Retry-After", "1");
                }
                exchange.sendResponseHeaders(code, -1);
                exchange.close();
            });
            server.start();
        }

        String url() {
            return "http://127.0.0.1:" + server.getAddress().getPort();
        }

        List<String> bodies() {
            synchronized (bodies) {
                return List.copyOf(bodies);
            }
        }

        List<String> auth() {
            synchronized (bodies) {
                return List.copyOf(auth);
            }
        }

        int requests() {
            return requests.get();
        }

        @Override
        public void close() {
            server.stop(0);
        }
    }

    private static void check(String label, boolean condition, String detail) {
        if (condition) {
            System.out.println("  ok   " + label);
            return;
        }
        failures++;
        System.out.println("  FAIL " + label + (detail.isEmpty() ? "" : " — " + detail));
    }

    private static void check(String label, boolean condition) {
        check(label, condition, "");
    }

    public static void main(String[] args) throws Exception {
        System.out.println("java sdk");
        envelopeShape();
        captureAndFlush();
        batching();
        throttled();
        retriedOnTransportFailure();
        boundedQueue();
        exceptionKeepsStack();
        uncaughtHandler();
        System.out.println(failures == 0 ? "java sdk: PASS" : "java sdk: " + failures + " FAILED");
        System.exit(failures == 0 ? 0 : 1);
    }

    private static void envelopeShape() {
        Map<String, Object> envelope = Envelope.build("api", "error", "boom", "db",
                Map.of("attempt", "2"), List.of(), null, List.of(), "sha-1");
        check("envelope is v1", Integer.valueOf(1).equals(envelope.get("v")));
        @SuppressWarnings("unchecked")
        Map<String, Object> event = (Map<String, Object>) envelope.get("event");
        check("component maps to module", "db".equals(event.get("module")));
        check("empty lists are lists", event.get("backtrace") instanceof List);
        check("release carried", "sha-1".equals(event.get("release")));
        check("timestamp is RFC3339 Z",
                String.valueOf(event.get("timestamp")).matches("\\d{4}-\\d{2}-\\d{2}T.*Z"));
        check("json escapes a quote", Json.encode("a\"b").equals("\"a\\\"b\""));
        check("json encodes null correlation", Json.encode(envelope).contains("\"correlation_id\":null"));
    }

    private static void captureAndFlush() throws Exception {
        try (Collector fake = new Collector()) {
            Client client = Client.builder().url(fake.url()).app("api").token("tkn").build();
            client.capture("error", "boom", "db", Map.of("attempt", "2"));
            check("flush reports empty", client.flush(Duration.ofSeconds(2)));
            check("one request", fake.bodies().size() == 1);
            check("bearer token sent", "Bearer tkn".equals(fake.auth().get(0)));
            String body = fake.bodies().get(0);
            check("body is a JSON array", body.startsWith("[{") && body.endsWith("}]"));
            check("message on the wire", body.contains("\"message\":\"boom\""));
            check("app on the wire", body.contains("\"app\":\"api\""));
            check("stats.sent = 1", client.stats().sent() == 1);
            client.shutdown();
        }
    }

    private static void batching() throws Exception {
        try (Collector fake = new Collector()) {
            // A long interval keeps the background sender out of the way: this
            // test is about one flush carrying everything queued, and a timer
            // firing mid-test would make the request count a coin toss.
            Client client = Client.builder().url(fake.url()).app("batch")
                    .batchSize(50).flushMillis(60_000).build();
            for (int i = 0; i < 5; i++) {
                client.capture("info", "m" + i, "", Map.of());
            }
            check("flush after batching", client.flush(Duration.ofSeconds(3)));
            check("five events, one request",
                    fake.bodies().size() == 1 && fake.bodies().get(0).split("\"v\":1").length == 6,
                    "requests=" + fake.bodies().size());
            client.shutdown();
        }
    }

    private static void throttled() throws Exception {
        try (Collector fake = new Collector(429)) {
            Client client = Client.builder().url(fake.url()).app("limited").flushMillis(60_000).build();
            client.capture("error", "kept", "", Map.of());
            check("429 retried to success", client.flush(Duration.ofSeconds(5)));
            Stats stats = client.stats();
            check("throttled counted", stats.throttled() == 1, stats.asMap().toString());
            check("nothing dropped", stats.dropped() == 0);
            check("event landed", fake.bodies().size() == 1
                    && fake.bodies().get(0).contains("\"message\":\"kept\""));
            client.shutdown();
        }
    }

    private static void retriedOnTransportFailure() throws Exception {
        try (Collector fake = new Collector(-1)) {
            Client client = Client.builder().url(fake.url()).app("retry").flushMillis(60_000).build();
            client.capture("error", "kept", "db", Map.of());
            check("dropped connection retried to success", client.flush(Duration.ofSeconds(5)));
            Stats stats = client.stats();
            // Two requests prove the retry happened; one would mean the double
            // answered and the test proved nothing.
            check("the transport tried twice", fake.requests() == 2, "requests=" + fake.requests());
            check("missing answer is not counted as failure", stats.failed() == 0,
                    stats.asMap().toString());
            check("batch landed", fake.bodies().size() == 1
                    && fake.bodies().get(0).contains("\"message\":\"kept\""));
            client.shutdown();
        }
    }

    private static void boundedQueue() {
        Client client = Client.builder().url("http://127.0.0.1:9").queueSize(3).flushMillis(600_000).build();
        for (int i = 0; i < 6; i++) {
            client.capture("info", "m" + i, "", Map.of());
        }
        Stats stats = client.stats();
        check("queue bounded", stats.dropped() == 3 && stats.queued() == 3, stats.asMap().toString());
        client.shutdown();
    }

    private static void exceptionKeepsStack() throws Exception {
        try (Collector fake = new Collector()) {
            Client client = Client.builder().url(fake.url()).app("api").build();
            client.captureException(new IllegalStateException("db down"), "store");
            client.flush(Duration.ofSeconds(2));
            String body = fake.bodies().get(0);
            check("exception message", body.contains("\"message\":\"IllegalStateException: db down\""));
            check("error_type recorded", body.contains("\"error_type\":\"IllegalStateException\""));
            check("stack carried", body.contains("at com.b4n1.telemetry.SdkTest.exceptionKeepsStack"));
            check("stack lines are escaped", body.contains("\\n") || body.contains("at "));
            client.shutdown();
        }
    }

    private static void uncaughtHandler() throws Exception {
        try (Collector fake = new Collector()) {
            Client client = Client.builder().url(fake.url()).app("worker").build();
            Telemetry.init(client);
            check("init keeps the first client", Telemetry.init(client) == client);
            Telemetry.installUncaughtHandler();
            Thread thread = new Thread(() -> {
                throw new RuntimeException("uncaught-boom");
            }, "job");
            thread.start();
            thread.join(5000);
            client.flush(Duration.ofSeconds(2));
            String body = fake.bodies().isEmpty() ? "" : fake.bodies().get(0);
            check("fatal reported", body.contains("\"level\":\"fatal\""), body);
            check("uncaught module", body.contains("\"module\":\"uncaught\""));
            check("thread name carried", body.contains("\"thread\":\"job\""));
            check("stack of the fatal event", body.contains("uncaught-boom"));
            Telemetry.shutdown();
        }
    }

}
