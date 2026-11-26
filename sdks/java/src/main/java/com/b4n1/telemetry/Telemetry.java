package com.b4n1.telemetry;

import java.time.Duration;
import java.util.Map;

/**
 * Drop-in integration for the JVM.
 *
 * <pre>{@code
 * Telemetry.init(Client.builder().app("api").build());
 * }</pre>
 *
 * <p>After that, {@code capture}/{@code captureException} report to the
 * collector; {@link #installUncaughtHandler()} additionally reports every
 * uncaught exception as fatal, which is the error that matters most and the
 * only one a plain library integration never sees.
 */
public final class Telemetry {
    private static volatile Client client;
    private static volatile String correlationId;

    private Telemetry() {
    }

    public static synchronized Client init(Client configured) {
        if (client == null) {
            client = configured;
        }
        return client;
    }

    public static Client client() {
        return client;
    }

    public static boolean capture(String level, String message, String component, Map<String, String> data) {
        Client current = client;
        return current != null && current.capture(level, message, component, data);
    }

    public static boolean captureException(Throwable error, String component) {
        Client current = client;
        return current != null && current.captureException(error, component);
    }

    public static boolean flush(Duration budget) {
        Client current = client;
        return current == null || current.flush(budget);
    }

    public static synchronized void shutdown() {
        if (client != null) {
            client.shutdown();
            client = null;
        }
    }

    /**
     * Correlation id for the work in progress. Server frameworks call this per
     * request; a thread-local keeps one request's id out of another's events,
     * which a static field alone would not do.
     */
    public static void withCorrelation(String id) {
        correlationId = id;
        CORRELATION.set(id);
    }

    public static String correlationId() {
        String id = CORRELATION.get();
        return id != null ? id : correlationId;
    }

    private static final ThreadLocal<String> CORRELATION = new ThreadLocal<>();

    /**
     * Report uncaught exceptions as fatal, then let the JVM's default handler
     * run so the process still dies the way its caller expects.
     */
    public static void installUncaughtHandler() {
        Thread.UncaughtExceptionHandler previous = Thread.getDefaultUncaughtExceptionHandler();
        Thread.setDefaultUncaughtExceptionHandler((thread, error) -> {
            Client current = client;
            if (current != null) {
                // One event, with the real stack: the stack is the whole point
                // of a fatal report.
                current.enqueue(Envelope.build(currentApp(), "fatal", Envelope.describe(error),
                        "uncaught",
                        Map.of("error_type", error.getClass().getSimpleName(),
                                "thread", thread.getName()),
                        Envelope.stackLines(error), null, null, null));
                current.flush(Duration.ofSeconds(2));
            }
            if (previous != null) {
                previous.uncaughtException(thread, error);
            }
        });
    }

    private static String currentApp() {
        Client current = client;
        return current == null ? "java" : Env.get("B4N1_TELEMETRY_APP", "java");
    }
}
