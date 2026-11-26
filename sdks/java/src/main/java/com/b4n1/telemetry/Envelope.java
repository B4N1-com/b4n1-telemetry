package com.b4n1.telemetry;

import java.io.PrintWriter;
import java.io.StringWriter;
import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;

/**
 * Wire envelope, field for field the collector's TelemetryEvent.
 *
 * <p>Built as a plain map so the shape is visible and testable without a JSON
 * library: the SDK has no dependencies, and serialization happens once in
 * {@link Json#encode(Object)}.
 */
public final class Envelope {
    public static final int VERSION = 1;
    public static final String DEFAULT_URL = "http://127.0.0.1:9450";
    private static final Set<String> LEVELS = Set.of("debug", "info", "warning", "error", "fatal");
    private static final DateTimeFormatter ISO =
            DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.ROOT);

    private Envelope() {
    }

    /** RFC 3339 UTC with milliseconds — the form the collector parses. */
    public static String nowIso() {
        return ISO.format(Instant.now().atOffset(ZoneOffset.UTC));
    }

    /**
     * Build one envelope. Every optional field gets its defined zero value
     * (null correlation id, empty lists) so a half-populated event never
     * reaches the wire.
     */
    public static Map<String, Object> build(
            String app,
            String level,
            String message,
            String component,
            Map<String, String> data,
            List<String> backtrace,
            String correlationId,
            List<Map<String, String>> breadcrumbs,
            String release) {
        if (!LEVELS.contains(level)) {
            throw new IllegalArgumentException("unknown level " + level);
        }
        Map<String, Object> event = new LinkedHashMap<>();
        event.put("timestamp", nowIso());
        event.put("app", app);
        event.put("level", level);
        event.put("message", message);
        event.put("module", component);
        event.put("data", data == null ? Map.of() : new LinkedHashMap<>(data));
        event.put("backtrace", backtrace == null ? List.of() : List.copyOf(backtrace));
        event.put("correlation_id", correlationId);
        event.put("breadcrumbs", breadcrumbs == null ? List.of() : List.copyOf(breadcrumbs));
        // Omitted when unset so an event from an older producer stays valid.
        if (release != null && !release.isEmpty()) {
            event.put("release", release);
        }
        Map<String, Object> envelope = new LinkedHashMap<>();
        envelope.put("v", VERSION);
        envelope.put("event", event);
        return envelope;
    }

    /** {@code "RuntimeException: boom"} — the message every other SDK uses. */
    public static String describe(Throwable error) {
        return error.getClass().getSimpleName() + ": " + String.valueOf(error.getMessage());
    }

    /** The real stack as one string per frame, like a Python traceback. */
    public static List<String> stackLines(Throwable error) {
        StringWriter writer = new StringWriter();
        error.printStackTrace(new PrintWriter(writer));
        List<String> lines = new ArrayList<>();
        for (String line : writer.toString().split("\n")) {
            if (!line.isBlank()) {
                lines.add(line.stripTrailing());
            }
        }
        return lines;
    }
}
