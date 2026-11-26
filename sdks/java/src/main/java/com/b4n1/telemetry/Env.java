package com.b4n1.telemetry;

/** Environment resolution shared by the client and the builder. */
final class Env {
    private Env() {
    }

    /** A non-empty environment value, or the fallback. */
    static String get(String key, String fallback) {
        String value = System.getenv(key);
        return value == null || value.isEmpty() ? fallback : value;
    }

    /** A non-empty environment value, or null when unset. */
    static String optional(String key) {
        String value = System.getenv(key);
        return value == null || value.isEmpty() ? null : value;
    }

    static String stripTrailingSlash(String value) {
        String out = value;
        while (out.endsWith("/")) {
            out = out.substring(0, out.length() - 1);
        }
        return out;
    }
}
