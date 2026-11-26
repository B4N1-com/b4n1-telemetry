package com.b4n1.telemetry;

import java.util.List;
import java.util.Map;

/**
 * Minimal JSON encoder for the SDK's own output.
 *
 * <p>Writing forty lines of {@code quote()} is the whole cost of keeping the
 * SDK dependency-free: an observability client that drags a JSON library into
 * every application's classpath is a dependency the user did not ask for.
 *
 * <p>Java 11 source level on purpose — a client library that needs a newer JDK
 * than the application's is a library that cannot be adopted.
 */
public final class Json {
    private Json() {
    }

    /** Encode a value tree: Map, List, String, Number, Boolean or null. */
    public static String encode(Object value) {
        StringBuilder out = new StringBuilder();
        write(out, value);
        return out.toString();
    }

    /** One JSON array of envelopes — the collector's batch body. */
    public static String encodeBatch(List<Map<String, Object>> envelopes) {
        StringBuilder out = new StringBuilder("[");
        for (int i = 0; i < envelopes.size(); i++) {
            if (i > 0) {
                out.append(',');
            }
            write(out, envelopes.get(i));
        }
        return out.append(']').toString();
    }

    private static void write(StringBuilder out, Object value) {
        if (value == null) {
            out.append("null");
        } else if (value instanceof String) {
            quote(out, (String) value);
        } else if (value instanceof Number || value instanceof Boolean) {
            out.append(value);
        } else if (value instanceof Map) {
            writeMap(out, (Map<?, ?>) value);
        } else if (value instanceof List) {
            writeList(out, (List<?>) value);
        } else {
            quote(out, String.valueOf(value));
        }
    }

    private static void writeMap(StringBuilder out, Map<?, ?> map) {
        out.append('{');
        boolean first = true;
        for (Map.Entry<?, ?> entry : map.entrySet()) {
            if (!first) {
                out.append(',');
            }
            first = false;
            quote(out, String.valueOf(entry.getKey()));
            out.append(':');
            write(out, entry.getValue());
        }
        out.append('}');
    }

    private static void writeList(StringBuilder out, List<?> items) {
        out.append('[');
        for (int i = 0; i < items.size(); i++) {
            if (i > 0) {
                out.append(',');
            }
            write(out, items.get(i));
        }
        out.append(']');
    }

    private static void quote(StringBuilder out, String value) {
        out.append('"');
        for (int i = 0; i < value.length(); i++) {
            char c = value.charAt(i);
            switch (c) {
                case '"':
                    out.append("\\\"");
                    break;
                case '\\':
                    out.append("\\\\");
                    break;
                case '\n':
                    out.append("\\n");
                    break;
                case '\r':
                    out.append("\\r");
                    break;
                case '\t':
                    out.append("\\t");
                    break;
                case '\b':
                    out.append("\\b");
                    break;
                case '\f':
                    out.append("\\f");
                    break;
                default:
                    if (c < 0x20) {
                        out.append(String.format("\\u%04x", (int) c));
                    } else {
                        out.append(c);
                    }
            }
        }
        out.append('"');
    }
}
