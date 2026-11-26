package com.b4n1.telemetry;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.List;
import java.util.Map;

/**
 * The POST itself, isolated so the queue and the retry policy stay readable on
 * their own. This is the only place that knows HTTP exists.
 *
 * <p>Uses {@code java.net.http}, in the JDK since 11, so the SDK adds no
 * dependency to the application's classpath.
 */
final class Transport {
    /** Wait before re-sending a request that never got an answer. */
    private static final long RETRY_PAUSE_MILLIS = 100;

    private final String url;
    private final String token;
    private final Duration timeout;
    private final HttpClient http;

    Transport(String url, String token, Duration timeout) {
        this.url = url;
        this.token = token;
        this.timeout = timeout;
        this.http = HttpClient.newBuilder().connectTimeout(timeout).build();
    }

    /** Outcome of one POST: the status, and the 429 wait when there is one. */
    static final class Delivery {
        final int status;
        final String retryAfter;
        final String error;

        Delivery(int status, String retryAfter, String error) {
            this.status = status;
            this.retryAfter = retryAfter;
            this.error = error;
        }
    }

    /** Retry-After in seconds; a missing or unparseable header means one. */
    static int retrySeconds(String value) {
        if (value == null) {
            return 1;
        }
        try {
            return Math.max(1, Integer.parseInt(value.trim()));
        } catch (NumberFormatException ignored) {
            return 1;
        }
    }

    /**
     * One POST, plus one retry when no answer arrived at all: a timeout against
     * a healthy collector means the collector is fsyncing, and failing there
     * loses the batch. An HTTP answer is returned untouched — a 429 has its own
     * requeue path and a 4xx would repeat the same mistake.
     */
    Delivery sendWithRetry(List<Map<String, Object>> batch) {
        Delivery delivery = send(batch);
        if (delivery.status >= 0 || "interrupted".equals(delivery.error)) {
            return delivery;
        }
        try {
            Thread.sleep(RETRY_PAUSE_MILLIS);
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
            return delivery;
        }
        return send(batch);
    }

    Delivery send(List<Map<String, Object>> batch) {
        try {
            HttpRequest.Builder request = HttpRequest.newBuilder(URI.create(url + "/api/v1/events"))
                    .timeout(timeout)
                    .header("Content-Type", "application/json")
                    .POST(HttpRequest.BodyPublishers.ofString(Json.encodeBatch(batch)));
            if (token != null) {
                request.header("Authorization", "Bearer " + token);
            }
            HttpResponse<String> response = http.send(request.build(),
                    HttpResponse.BodyHandlers.ofString());
            return new Delivery(response.statusCode(),
                    response.headers().firstValue("Retry-After").orElse(null), null);
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
            return new Delivery(-1, null, "interrupted");
        } catch (Exception failure) {
            return new Delivery(-1, null,
                    failure.getClass().getSimpleName() + ": " + failure.getMessage());
        }
    }
}
