<?php

declare(strict_types=1);

namespace B4n1\Telemetry;

/**
 * Drop-in integration for PHP.
 *
 *     \B4n1\Telemetry\Telemetry::init(['app' => 'api']);
 *
 * After that, capture()/captureException() report to the collector and the
 * queue is flushed when the request ends. installErrorHandlers() additionally
 * turns uncaught exceptions and fatal errors into events — call it once, early.
 *
 * Environment: B4N1_TELEMETRY_URL, B4N1_TELEMETRY_TOKEN, B4N1_TELEMETRY_APP,
 * B4N1_TELEMETRY_RELEASE.
 */
final class Telemetry
{
    private static ?Client $client = null;
    private static bool $shutdownRegistered = false;
    private static ?string $correlationId = null;

    /** @param array<string,mixed> $options */
    public static function init(array $options = []): Client
    {
        if (self::$client === null) {
            self::$client = new Client($options);
            if (!self::$shutdownRegistered) {
                self::$shutdownRegistered = true;
                // PHP has no background thread: the end of the request is the
                // only reliable moment to send what is queued.
                register_shutdown_function(static function (): void {
                    self::shutdown();
                });
            }
        }

        return self::$client;
    }

    public static function client(): ?Client
    {
        return self::$client;
    }

    /** @param array<string,mixed> $data */
    public static function capture(
        string $message,
        string $level = 'error',
        string $component = '',
        array $data = []
    ): bool {
        if (self::$client === null) {
            return false;
        }

        return self::$client->capture($message, $level, $component, $data, [], self::$correlationId);
    }

    public static function captureException(\Throwable $error, string $component = ''): bool
    {
        return self::$client !== null && self::$client->captureException($error, $component);
    }

    public static function flush(float $timeout = 2.0): bool
    {
        return self::$client === null || self::$client->flush($timeout);
    }

    public static function shutdown(float $timeout = 2.0): void
    {
        if (self::$client !== null) {
            self::$client->shutdown($timeout);
            self::$client = null;
        }
    }

    /**
     * Correlation id for the work in progress. PHP requests are one process
     * each, so a static field is the request scope, not a global leak.
     */
    public static function withCorrelation(?string $id): void
    {
        self::$correlationId = $id;
    }

    /**
     * Report uncaught exceptions and fatal errors. Without this, the errors
     * that matter most — the ones that killed the request — are the only ones
     * the collector never hears about.
     */
    public static function installErrorHandlers(): void
    {
        set_exception_handler(static function (\Throwable $error): void {
            self::captureException($error, 'uncaught');
            self::flush(2.0);
            fwrite(STDERR, $error . PHP_EOL);
        });

        register_shutdown_function(static function (): void {
            $last = error_get_last();
            if ($last === null || !in_array($last['type'], [E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR], true)) {
                return;
            }
            if (self::$client === null) {
                return;
            }
            self::$client->capture(
                $last['message'],
                'fatal',
                'shutdown',
                ['file' => $last['file'], 'line' => (string) $last['line']]
            );
            self::$client->flush(2.0);
        });
    }
}
