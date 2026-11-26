package com.b4n1.telemetry;

import java.time.Duration;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Deque;
import java.util.List;
import java.util.Map;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

/**
 * Bounded queue drained by a background sender.
 *
 * <p>Sending never blocks the application and never throws: a telemetry client
 * that breaks its host turns a bug report into an outage. Failures are counted
 * in {@link Stats}; the one exception is a 429, where the batch goes back to
 * the head of the queue and sending pauses for Retry-After — retrying at once
 * would earn another 429 and dropping it would lose data to a quota the
 * producer never chose.
 */
public final class Client {
    private final String url;
    private final String token;
    private final String app;
    private final String release;
    private final int queueSize;
    private final int batchSize;
    private final Duration timeout;
    private final Transport transport;
    private final ScheduledExecutorService sender;

    private final Deque<Map<String, Object>> queue = new ArrayDeque<>();
    private int sent;
    private int failed;
    private int dropped;
    private int throttled;
    private int inFlight;
    private String lastError;
    private long pausedUntilMillis;
    private boolean closed;

    Client(ClientBuilder builder) {
        this.url = builder.resolvedUrl();
        this.token = builder.resolvedToken();
        this.app = builder.resolvedApp();
        this.release = builder.resolvedRelease();
        this.queueSize = builder.queue();
        this.batchSize = builder.batch();
        this.timeout = Duration.ofMillis(builder.timeout());
        this.transport = new Transport(this.url, this.token, this.timeout);
        this.sender = Executors.newSingleThreadScheduledExecutor(runnable -> {
            Thread thread = new Thread(runnable, "b4n1-telemetry-sender");
            thread.setDaemon(true);
            return thread;
        });
        long interval = builder.interval();
        this.sender.scheduleWithFixedDelay(() -> flush(Duration.ofSeconds(2)),
                interval, interval, TimeUnit.MILLISECONDS);
    }

    public static ClientBuilder builder() {
        return new ClientBuilder();
    }

    /** Enqueue one event. Returns false when the queue is full (counted). */
    public boolean capture(String level, String message, String component, Map<String, String> data) {
        return enqueue(Envelope.build(app, level, message, component, data, null, null, null, release));
    }

    /** Report an exception at level "error", with its real stack. */
    public boolean captureException(Throwable error, String component) {
        if (error == null) {
            return false;
        }
        return enqueue(Envelope.build(app, "error", Envelope.describe(error), component,
                Map.of("error_type", error.getClass().getSimpleName()),
                Envelope.stackLines(error), null, null, release));
    }

    public synchronized boolean enqueue(Map<String, Object> envelope) {
        if (closed) {
            return false;
        }
        if (queue.size() >= queueSize) {
            dropped++;
            return false;
        }
        queue.addLast(envelope);
        return true;
    }

    /**
     * Sends everything queued. True when nothing is pending afterwards; an
     * event already in flight counts as pending, because reporting "flushed"
     * while the POST is open is how a caller loses data.
     */
    public boolean flush(Duration budget) {
        long deadline = System.nanoTime() + budget.toNanos();
        while (true) {
            List<Map<String, Object>> batch = take();
            if (!batch.isEmpty()) {
                post(batch);
                continue;
            }
            if (pending() == 0) {
                return true;
            }
            if (System.nanoTime() > deadline) {
                return false;
            }
            sleep();
        }
    }

    private void sleep() {
        long pause = pausedUntilMillis - System.currentTimeMillis();
        long millis = Math.max(10, Math.min(pause > 0 ? pause : 10, 100));
        try {
            Thread.sleep(millis);
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
        }
    }

    public void shutdown() {
        flush(Duration.ofSeconds(2));
        synchronized (this) {
            closed = true;
        }
        sender.shutdownNow();
    }

    public synchronized Stats stats() {
        return new Stats(url, sent, failed, dropped, throttled, queue.size());
    }

    public synchronized int pending() {
        return queue.size() + inFlight;
    }

    private synchronized List<Map<String, Object>> take() {
        if (queue.isEmpty() || closed || System.currentTimeMillis() < pausedUntilMillis) {
            return List.of();
        }
        List<Map<String, Object>> batch = new ArrayList<>();
        while (batch.size() < batchSize && !queue.isEmpty()) {
            batch.add(queue.pollFirst());
        }
        inFlight += batch.size();
        return batch;
    }

    private void post(List<Map<String, Object>> batch) {
        Transport.Delivery delivery = transport.sendWithRetry(batch);
        settle(batch, delivery.status, delivery.retryAfter);
        if (delivery.error != null) {
            synchronized (this) {
                lastError = delivery.error;
            }
        }
    }

    private void settle(List<Map<String, Object>> batch, int status, String retryAfter) {
        synchronized (this) {
            inFlight -= batch.size();
            if (status >= 200 && status < 300) {
                sent++;
            } else if (status == 429) {
                throttled++;
                int seconds = Transport.retrySeconds(retryAfter);
                pausedUntilMillis = System.currentTimeMillis() + seconds * 1000L;
                lastError = "HTTP 429 (retry in " + seconds + "s)";
                requeue(batch);
            } else if (status > 0) {
                failed++;
                lastError = "HTTP " + status;
            } else {
                failed++;
            }
        }
    }

    /** The refused batch is the oldest: it goes back first, then what fits. */
    private void requeue(List<Map<String, Object>> batch) {
        List<Map<String, Object>> restored = new ArrayList<>(batch);
        restored.addAll(queue);
        queue.clear();
        for (Map<String, Object> envelope : restored) {
            if (queue.size() < queueSize) {
                queue.addLast(envelope);
            } else {
                dropped++;
            }
        }
    }

}
