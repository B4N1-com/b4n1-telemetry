<?php

declare(strict_types=1);

namespace B4n1\Telemetry;

/**
 * The POST itself, isolated so the queue and the retry policy stay readable on
 * their own. This is the only place that knows HTTP exists.
 *
 * Uses the stream wrapper instead of curl: a client library that requires an
 * extension the host may not have is a library that cannot be adopted.
 */
final class Transport
{
    public function __construct(
        private readonly string $url,
        private readonly ?string $token,
        private readonly float $timeout
    ) {
    }

    /**
     * @param list<array{v:int,event:array<string,mixed>}> $batch
     * @return array{status:int,retry_after:float} status 0 means the request never got an answer
     */
    public function send(array $batch): array
    {
        $body = json_encode($batch, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
        if ($body === false) {
            return ['status' => 0, 'retry_after' => 0.0];
        }
        $headers = "Content-Type: application/json\r\n";
        if ($this->token !== null) {
            $headers .= 'Authorization: Bearer ' . $this->token . "\r\n";
        }
        $context = stream_context_create(['http' => [
            'method' => 'POST',
            'header' => $headers,
            'content' => $body,
            'timeout' => $this->timeout,
            'ignore_errors' => true, // read the status instead of a warning
        ]]);

        $response = @file_get_contents($this->url . '/api/v1/events', false, $context);
        $statusLine = $http_response_header[0] ?? '';
        $status = preg_match('#\s(\d{3})\s#', (string) $statusLine, $match) === 1 ? (int) $match[1] : 0;
        if ($response === false && $status === 0) {
            return ['status' => 0, 'retry_after' => 0.0];
        }
        if ($status === 429) {
            return ['status' => 429, 'retry_after' => $this->retryAfter($http_response_header ?? [])];
        }

        return ['status' => $status, 'retry_after' => 0.0];
    }

    /** @param list<string> $headers */
    private function retryAfter(array $headers): float
    {
        foreach ($headers as $header) {
            if (stripos($header, 'retry-after:') === 0) {
                $seconds = (int) trim(substr($header, strlen('retry-after:')));

                return $seconds > 0 ? (float) $seconds : 1.0;
            }
        }

        return 1.0;
    }
}
