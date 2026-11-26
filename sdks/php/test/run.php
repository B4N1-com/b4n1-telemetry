<?php

declare(strict_types=1);

/**
 * Offline suite: a real local HTTP server (forked child), real envelopes, no
 * network. Run with `php test/run.php`.
 */

require __DIR__ . '/../src/Envelope.php';
require __DIR__ . '/../src/Transport.php';
require __DIR__ . '/../src/Client.php';
require __DIR__ . '/../src/Telemetry.php';

use B4n1\Telemetry\Client;
use B4n1\Telemetry\Envelope;
use B4n1\Telemetry\Telemetry;

$failures = 0;

function check(string $label, bool $condition, string $detail = ''): void
{
    global $failures;
    if ($condition) {
        echo "  ok   {$label}\n";
        return;
    }
    $failures++;
    echo "  FAIL {$label}" . ($detail !== '' ? " — {$detail}" : '') . "\n";
}

/**
 * Forked collector double: answers the protocol, stores what it accepts.
 *
 * @param list<int> $statuses status codes to answer, consumed in order
 * @return array{url:string,batches:callable,auth:callable,stop:callable}
 */
function collector(array $statuses = []): array
{
    $server = stream_socket_server('tcp://127.0.0.1:0', $errno, $error);
    if ($server === false) {
        throw new RuntimeException("cannot bind collector: {$error}");
    }
    $name = stream_socket_get_name($server, false);
    $port = (int) substr((string) $name, strrpos((string) $name, ':') + 1);
    $pid = pcntl_fork();
    if ($pid === 0) {
        // Child: serve until the parent closes the pipe.
        while (true) {
            $conn = @stream_socket_accept($server, 5);
            if ($conn === false) {
                continue;
            }
            $requestLine = fgets($conn);
            $headers = [];
            while (($line = fgets($conn)) !== false && trim($line) !== '') {
                [$key, $value] = array_pad(explode(':', $line, 2), 2, '');
                $headers[strtolower(trim($key))] = trim($value);
            }
            $body = '';
            $length = (int) ($headers['content-length'] ?? 0);
            while (strlen($body) < $length) {
                $chunk = fread($conn, $length - strlen($body));
                if ($chunk === false || $chunk === '') {
                    break;
                }
                $body .= $chunk;
            }
            $status = $statuses === [] ? 202 : array_shift($statuses);
            if ($status < 0) {
                // No answer at all: the transport failure the retry exists for.
                fclose($conn);
                continue;
            }
            if ($status < 300) {
                file_put_contents(
                    sys_get_temp_dir() . "/b4n1-php-collector-{$port}.jsonl",
                    json_encode(['auth' => $headers['authorization'] ?? null, 'body' => $body]) . "\n",
                    FILE_APPEND
                );
            }
            $extra = $status === 429 ? "Retry-After: 1\r\n" : '';
            fwrite($conn, "HTTP/1.1 {$status} X\r\n{$extra}Content-Length: 0\r\nConnection: close\r\n\r\n");
            fclose($conn);
        }
    }
    $file = sys_get_temp_dir() . "/b4n1-php-collector-{$port}.jsonl";

    return [
        'url' => "http://127.0.0.1:{$port}",
        'received' => static fn (): array => is_file($file)
            ? array_map(static fn ($line) => json_decode($line, true), file($file, FILE_IGNORE_NEW_LINES))
            : [],
        'events' => static fn (): array => array_merge(...array_map(
            static fn ($entry) => array_map(static fn ($env) => $env['event'], json_decode($entry['body'], true)),
            is_file($file) ? array_map(static fn ($line) => json_decode($line, true), file($file, FILE_IGNORE_NEW_LINES)) : []
        ) ?: []),
        'stop' => static function () use ($pid, $file, $server): void {
            posix_kill($pid, SIGTERM);
            pcntl_waitpid($pid, $status);
            fclose($server);
            @unlink($file);
        },
    ];
}

echo "php sdk\n";

// 1. Envelope shape is the collector's, field for field.
$envelope = Envelope::build(app: 'api', message: 'boom', component: 'db', data: ['attempt' => 2], release: 'sha-1');
check('envelope is v1', $envelope['v'] === 1);
check('data travels as strings', $envelope['event']['data']['attempt'] === '2');
check('component maps to module', $envelope['event']['module'] === 'db');
check('empty lists are lists', $envelope['event']['backtrace'] === []);
check('release is carried', $envelope['event']['release'] === 'sha-1');
check('timestamp is RFC3339 Z', (bool) preg_match('/^\d{4}-\d{2}-\d{2}T.*Z$/', $envelope['event']['timestamp']));

// 2. A real POST lands with the right auth and body.
$fake = collector();
$client = new Client(['url' => $fake['url'], 'app' => 'api', 'token' => 'tkn']);
$client->capture('hello', 'error', 'checkout', ['order' => 42]);
check('flush reports empty', $client->flush(2.0));
$received = $fake['received']();
check('one request', count($received) === 1, 'got ' . count($received));
check('bearer token sent', ($received[0]['auth'] ?? '') === 'Bearer tkn');
$events = $fake['events']();
check('one event stored', count($events) === 1);
check('message matches', ($events[0]['message'] ?? '') === 'hello');
check('module matches', ($events[0]['module'] ?? '') === 'checkout');
check('stats.sent = 1', $client->stats()['sent'] === 1);
$fake['stop']();

// 3. An exception keeps its class and backtrace.
$fake = collector();
$client = new Client(['url' => $fake['url'], 'app' => 'api']);
$client->captureException(new InvalidArgumentException('bad input'), 'checkout');
$client->flush(2.0);
$events = $fake['events']();
check('exception message', ($events[0]['message'] ?? '') === 'InvalidArgumentException: bad input');
check('error_type recorded', ($events[0]['data']['error_type'] ?? '') === 'InvalidArgumentException');
check('backtrace carried', count($events[0]['backtrace'] ?? []) > 0);
$fake['stop']();

// 4. A 429 is retried, never dropped.
$fake = collector([429]);
$client = new Client(['url' => $fake['url'], 'app' => 'limited']);
$client->capture('kept');
check('429 retried to success', $client->flush(5.0));
$stats = $client->stats();
check('throttled counted', $stats['throttled'] === 1);
check('nothing dropped', $stats['dropped'] === 0);
check('event landed', count($fake['events']()) === 1);
$fake['stop']();

// 5. A full queue drops and counts instead of growing.
$client = new Client(['url' => 'http://127.0.0.1:9', 'queue_size' => 3]);
for ($i = 0; $i < 6; $i++) {
    $client->capture("m{$i}");
}
check('queue bounded', $client->stats()['dropped'] === 3 && $client->stats()['queued'] === 3);

// 6. The static facade flushes on shutdown.
$fake = collector();
Telemetry::init(['url' => $fake['url'], 'app' => 'facade']);
Telemetry::withCorrelation('req-7');
Telemetry::capture('from the facade');
check('facade flush', Telemetry::flush(2.0));
$events = $fake['events']();
check('facade event stored', count($events) === 1);
check('correlation carried', ($events[0]['correlation_id'] ?? null) === 'req-7');
Telemetry::shutdown(0.2);
$fake['stop']();

// 7. Wrong token is a 401, not a silent success.
$fake = collector([401]);
$client = new Client(['url' => $fake['url'], 'app' => 'auth', 'token' => 'wrong']);
$client->capture('rejected');
$client->flush(2.0);
$stats = $client->stats();
check('401 counted as failure', $stats['failed'] === 1 && $stats['sent'] === 0, json_encode($stats));
$fake['stop']();

// 8. A dropped connection is retried once instead of losing the batch.
$fake = collector([-1]);
$client = new Client(['url' => $fake['url'], 'app' => 'retry']);
$client->capture('kept');
check('dropped connection flushed', $client->flush(5.0));
$stats = $client->stats();
check(
    'a missing answer is retried, not counted',
    $stats['sent'] === 1 && $stats['failed'] === 0,
    json_encode($stats)
);
check('the batch landed', count($fake['events']()) === 1);
$fake['stop']();

echo $failures === 0 ? "php sdk: PASS\n" : "php sdk: {$failures} FAILED\n";
exit($failures === 0 ? 0 : 1);
