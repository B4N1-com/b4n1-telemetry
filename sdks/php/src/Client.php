<?php

declare(strict_types=1);

namespace B4n1\Telemetry;

/**
 * Bounded queue plus a synchronous sender.
 *
 * PHP's request model has no background thread, so events are sent at the end
 * of the request (`shutdown()`, or the automatic shutdown hook installed by
 * Telemetry::init). A bounded queue means a flood is dropped and counted
 * instead of turning into unbounded memory. Nothing here ever throws into the
 * application: a telemetry client that breaks its host is worse than none.
 */
final class Client
{
    private const RETRY_PAUSE_US = 100_000;

    public string $url;
    public string $app;
    public ?string $release;

    private ?string $token;
    private Transport $transport;
    private int $queueSize;
    private int $batchSize;
    private float $timeout;

    /** @var list<array{v:int,event:array<string,mixed>}> */
    private array $queue = [];
    private int $sent = 0;
    private int $failed = 0;
    private int $dropped = 0;
    private int $throttled = 0;
    private ?string $lastError = null;
    private bool $closed = false;

    /** @param array<string,mixed> $options */
    public function __construct(array $options = [])
    {
        $this->url = rtrim((string) ($options['url'] ?? getenv('B4N1_TELEMETRY_URL') ?: Envelope::DEFAULT_URL), '/');
        $token = $options['token'] ?? getenv('B4N1_TELEMETRY_TOKEN');
        $this->token = $token === false || $token === null || $token === '' ? null : (string) $token;
        $this->app = (string) ($options['app'] ?? getenv('B4N1_TELEMETRY_APP') ?: 'php');
        $release = $options['release'] ?? getenv('B4N1_TELEMETRY_RELEASE');
        $this->release = $release === false || $release === null || $release === '' ? null : (string) $release;
        $this->queueSize = (int) ($options['queue_size'] ?? 2048);
        $this->batchSize = max(1, (int) ($options['batch_size'] ?? 100));
        $this->timeout = (float) ($options['timeout'] ?? 3.0);
        $this->transport = new Transport($this->url, $this->token, $this->timeout);
    }

    /**
     * @param array<string,mixed> $data
     * @param list<string> $backtrace
     */
    public function capture(
        string $message,
        string $level = 'error',
        string $component = '',
        array $data = [],
        array $backtrace = [],
        ?string $correlationId = null
    ): bool {
        return $this->enqueue(Envelope::build(
            app: $this->app,
            level: $level,
            message: $message,
            component: $component,
            data: $data,
            backtrace: $backtrace,
            correlationId: $correlationId,
            release: $this->release
        ));
    }

    /** Report an exception at level "error", with its real backtrace. */
    public function captureException(\Throwable $error, string $component = ''): bool
    {
        return $this->enqueue(Envelope::build(
            app: $this->app,
            level: 'error',
            message: Envelope::describe($error),
            component: $component,
            data: ['error_type' => $error::class],
            backtrace: Envelope::backtraceLines($error),
            release: $this->release
        ));
    }

    /** @param array{v:int,event:array<string,mixed>} $envelope */
    public function enqueue(array $envelope): bool
    {
        if ($this->closed) {
            return false;
        }
        if (count($this->queue) >= $this->queueSize) {
            $this->dropped++;
            return false;
        }
        $this->queue[] = $envelope;

        return true;
    }

    /** Sends everything queued. True when the queue is empty afterwards. */
    public function flush(float $timeout = 2.0): bool
    {
        $deadline = microtime(true) + $timeout;
        while ($this->queue !== []) {
            $batch = array_splice($this->queue, 0, $this->batchSize);
            $pause = $this->post($batch);
            if ($pause > 0.0) {
                // The collector asked for time; the batch goes back at the head
                // and we wait, because retrying at once earns another 429.
                array_splice($this->queue, 0, 0, $batch);
                if (microtime(true) + $pause > $deadline) {
                    return false;
                }
                usleep((int) ($pause * 1_000_000));
            } elseif (microtime(true) > $deadline) {
                return false;
            }
        }

        return true;
    }

    public function shutdown(float $timeout = 2.0): void
    {
        $this->flush($timeout);
        $this->closed = true;
    }

    /** @return array<string,mixed> */
    public function stats(): array
    {
        return [
            'url' => $this->url,
            'sent' => $this->sent,
            'failed' => $this->failed,
            'dropped' => $this->dropped,
            'throttled' => $this->throttled,
            'queued' => count($this->queue),
            'last_error' => $this->lastError,
        ];
    }

    /**
     * POST one batch and fold the answer into the counters.
     *
     * @param list<array{v:int,event:array<string,mixed>}> $batch
     * @return float seconds to wait, 0 when there is nothing to wait for
     */
    private function post(array $batch): float
    {
        $answer = $this->transport->send($batch);
        if ($answer['status'] === 0) {
            // One retry, and only when no answer arrived: a timeout against a
            // healthy collector means the collector is fsyncing, and failing
            // here loses the batch. An HTTP answer is never retried — a 429
            // has its own path and a 4xx would repeat the same mistake.
            usleep(self::RETRY_PAUSE_US);
            $answer = $this->transport->send($batch);
        }
        $status = $answer['status'];

        if ($status >= 200 && $status < 300) {
            $this->sent++;
            return 0.0;
        }
        if ($status === 429) {
            $this->throttled++;
            $this->lastError = 'HTTP 429';
            return $answer['retry_after'];
        }
        $this->failed++;
        $this->lastError = $status === 0 ? 'transport error' : 'HTTP ' . $status;

        return 0.0;
    }
}
