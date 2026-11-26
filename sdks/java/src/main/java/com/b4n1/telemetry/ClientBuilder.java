package com.b4n1.telemetry;

/**
 * Builder for {@link Client}. The zero-configuration path reads the
 * B4N1_TELEMETRY_URL / _TOKEN / _APP / _RELEASE environment variables.
 */
public final class ClientBuilder {
    private String url;
    private String token;
    private String app;
    private String release;
    private int queueSize = 2048;
    private int batchSize = 100;
    private long timeoutMillis = 3000;
    private long flushMillis = 250;

    public ClientBuilder url(String value) {
        this.url = value;
        return this;
    }

    public ClientBuilder token(String value) {
        this.token = value;
        return this;
    }

    public ClientBuilder app(String value) {
        this.app = value;
        return this;
    }

    public ClientBuilder release(String value) {
        this.release = value;
        return this;
    }

    public ClientBuilder queueSize(int value) {
        this.queueSize = value;
        return this;
    }

    public ClientBuilder batchSize(int value) {
        this.batchSize = value;
        return this;
    }

    public ClientBuilder timeoutMillis(long value) {
        this.timeoutMillis = value;
        return this;
    }

    /** How often the background sender drains the queue. */
    public ClientBuilder flushMillis(long value) {
        this.flushMillis = value;
        return this;
    }

    public Client build() {
        return new Client(this);
    }

    String resolvedUrl() {
        return Env.stripTrailingSlash(url != null ? url
                : Env.get("B4N1_TELEMETRY_URL", Envelope.DEFAULT_URL));
    }

    String resolvedToken() {
        return token != null ? token : Env.optional("B4N1_TELEMETRY_TOKEN");
    }

    String resolvedApp() {
        return app != null ? app : Env.get("B4N1_TELEMETRY_APP", "java");
    }

    String resolvedRelease() {
        return release != null ? release : Env.optional("B4N1_TELEMETRY_RELEASE");
    }

    int queue() {
        return queueSize;
    }

    int batch() {
        return Math.max(1, batchSize);
    }

    long timeout() {
        return timeoutMillis;
    }

    long interval() {
        return Math.max(10, flushMillis);
    }
}
