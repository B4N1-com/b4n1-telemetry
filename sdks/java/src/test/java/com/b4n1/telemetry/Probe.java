package com.b4n1.telemetry;

import java.time.Duration;
import java.util.Map;

/**
 * Sends one event to a real collector, so a cross-language suite can prove the
 * Java SDK speaks the same wire format as every other SDK.
 */
public final class Probe {
    private Probe() {
    }

    public static void main(String[] args) {
        String url = System.getenv("B4N1_PROBE_URL");
        String app = System.getenv("B4N1_PROBE_APP");
        String token = System.getenv("B4N1_PROBE_TOKEN");
        for (String arg : args) {
            if (arg.startsWith("--url=")) {
                url = arg.substring("--url=".length());
            } else if (arg.startsWith("--app=")) {
                app = arg.substring("--app=".length());
            } else if (arg.startsWith("--token=")) {
                token = arg.substring("--token=".length());
            }
        }
        if (url == null || app == null) {
            System.err.println("probe: B4N1_PROBE_URL and B4N1_PROBE_APP are required");
            System.exit(2);
        }

        Client client = Client.builder().url(url).app(app).token(token)
                // 10s, not the 3s default: this probe verifies the wire
                // format, not the timeout budget of a cold JVM.
                .timeoutMillis(10_000).build();
        client.capture("error", "hello-from-" + app, "probe", Map.of("lang", "java"));
        boolean flushed = client.flush(Duration.ofSeconds(20));
        Stats stats = client.stats();
        client.shutdown();
        System.out.println("sent=" + stats.sent() + " failed=" + stats.failed()
                + " dropped=" + stats.dropped());
        if (!flushed || stats.sent() == 0) {
            System.exit(1);
        }
    }
}
