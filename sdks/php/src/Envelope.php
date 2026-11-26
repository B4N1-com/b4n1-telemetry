<?php

declare(strict_types=1);

namespace B4n1\Telemetry;

/**
 * Wire envelope, field for field the collector's TelemetryEvent.
 *
 * Every optional field gets its defined zero value here (null correlation id,
 * empty lists) so a half-populated event never reaches the wire.
 */
final class Envelope
{
    public const VERSION = 1;
    public const DEFAULT_URL = 'http://127.0.0.1:9450';
    public const LEVELS = ['debug', 'info', 'warning', 'error', 'fatal'];

    /** RFC 3339 UTC with milliseconds — the form the collector parses. */
    public static function nowIso(): string
    {
        return (new \DateTimeImmutable('now', new \DateTimeZone('UTC')))
            ->format('Y-m-d\TH:i:s.v\Z');
    }

    /**
     * @param array<string,mixed> $data
     * @param list<string> $backtrace
     * @param list<array<string,string>> $breadcrumbs
     * @return array{v:int,event:array<string,mixed>}
     */
    public static function build(
        string $app,
        string $level = 'error',
        string $message = '',
        string $component = '',
        array $data = [],
        array $backtrace = [],
        ?string $correlationId = null,
        array $breadcrumbs = [],
        ?string $timestamp = null,
        ?string $release = null
    ): array {
        if (!in_array($level, self::LEVELS, true)) {
            throw new \InvalidArgumentException("unknown level {$level}");
        }

        $event = [
            'timestamp' => $timestamp ?? self::nowIso(),
            'app' => $app,
            'level' => $level,
            'message' => $message,
            'module' => $component,
            'data' => self::stringify($data),
            'backtrace' => array_values($backtrace),
            'correlation_id' => $correlationId,
            'breadcrumbs' => array_values($breadcrumbs),
        ];
        // Omitted when unset so an event from an older producer stays valid.
        if ($release !== null && $release !== '') {
            $event['release'] = $release;
        }

        return ['v' => self::VERSION, 'event' => $event];
    }

    /**
     * The wire carries strings only: 2 and "2" must group together on the
     * dashboard instead of splitting one issue in two.
     *
     * @param array<string,mixed> $data
     * @return array<string,string>
     */
    public static function stringify(array $data): array
    {
        $out = [];
        foreach ($data as $key => $value) {
            $out[(string) $key] = is_bool($value) ? ($value ? 'true' : 'false') : (string) $value;
        }

        return $out;
    }

    /** `"RuntimeException: boom"` — the message every other SDK uses. */
    public static function describe(\Throwable $error): string
    {
        return $error::class . ': ' . $error->getMessage();
    }

    /**
     * A PHP backtrace as one string per frame, like a Python traceback.
     *
     * @return list<string>
     */
    public static function backtraceLines(\Throwable $error): array
    {
        // Frame 0 is where the exception came into being. An exception that
        // was never thrown has no getTrace() frames at all, and without this
        // line its origin would be lost entirely.
        $lines = [sprintf(
            '#0 %s at %s:%d',
            $error::class,
            $error->getFile(),
            $error->getLine()
        )];
        foreach ($error->getTrace() as $index => $frame) {
            $where = ($frame['file'] ?? '?') . ':' . ($frame['line'] ?? '?');
            $func = ($frame['class'] ?? '') . ($frame['type'] ?? '') . ($frame['function'] ?? '');
            $lines[] = sprintf('#%d %s %s()', $index + 1, $where, $func);
        }

        return $lines;
    }
}
