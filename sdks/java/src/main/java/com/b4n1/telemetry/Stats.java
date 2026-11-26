package com.b4n1.telemetry;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * What the client did, so an application can see the SDK was working without
 * reading logs. Failures are counted here, never thrown.
 *
 * <p>A plain immutable class rather than a record: the SDK targets Java 11 so
 * it can be adopted by applications that have not moved to a newer JDK.
 */
public final class Stats {
    private final String url;
    private final int sent;
    private final int failed;
    private final int dropped;
    private final int throttled;
    private final int queued;

    Stats(String url, int sent, int failed, int dropped, int throttled, int queued) {
        this.url = url;
        this.sent = sent;
        this.failed = failed;
        this.dropped = dropped;
        this.throttled = throttled;
        this.queued = queued;
    }

    public String url() {
        return url;
    }

    /** Batches accepted by the collector. */
    public int sent() {
        return sent;
    }

    /** Batches the collector rejected or that never arrived. */
    public int failed() {
        return failed;
    }

    /** Events lost to a full queue — visible instead of silent. */
    public int dropped() {
        return dropped;
    }

    /** Batches the collector asked us to slow down. */
    public int throttled() {
        return throttled;
    }

    public int queued() {
        return queued;
    }

    public Map<String, Object> asMap() {
        Map<String, Object> out = new LinkedHashMap<>();
        out.put("url", url);
        out.put("sent", sent);
        out.put("failed", failed);
        out.put("dropped", dropped);
        out.put("throttled", throttled);
        out.put("queued", queued);
        return out;
    }
}
