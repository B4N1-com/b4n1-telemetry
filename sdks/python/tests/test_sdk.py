"""Offline SDK suite: a real local HTTP server, real envelopes, no network."""

from __future__ import annotations

import json
import logging
import os
import sys
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import b4n1_telemetry as bt  # noqa: E402
from b4n1_telemetry import auto  # noqa: E402
from b4n1_telemetry.django import TelemetryMiddleware  # noqa: E402


class _Handler(BaseHTTPRequestHandler):
    def do_POST(self):  # noqa: N802 - http.server API
        length = int(self.headers.get("Content-Length", "0"))
        body = self.rfile.read(length)
        if self.server.drops:
            # No answer at all: what a stalled collector looks like on the wire.
            self.server.drops -= 1
            self.close_connection = True
            return
        self.server.received.append(
            (self.path, self.headers.get("Authorization"), body)
        )
        self.send_response(202)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def log_message(self, *args):  # silence the test server
        pass


class Collector:
    """Local collector double; same protocol as the real one."""

    def __init__(self, drops: int = 0) -> None:
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), _Handler)
        self.server.received = []
        self.server.drops = drops
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.url = "http://127.0.0.1:%d" % self.server.server_address[1]

    def envelopes(self):
        out = []
        for _path, _auth, body in self.server.received:
            out.extend(json.loads(body))
        return out

    def stop(self) -> None:
        self.server.shutdown()
        self.server.server_close()


class SdkTest(unittest.TestCase):
    def setUp(self) -> None:
        self.collector = Collector()
        self.client = bt.init(
            app="unit-app", url=self.collector.url, level=logging.ERROR
        )

    def tearDown(self) -> None:
        bt.shutdown()
        self.collector.stop()

    def test_init_is_idempotent_and_wires_one_handler(self) -> None:
        again = bt.init(app="other")
        self.assertIs(again, self.client)
        handlers = [h for h in logging.getLogger().handlers if isinstance(h, auto.TelemetryHandler)]
        self.assertEqual(len(handlers), 1)

    def test_capture_sends_a_valid_v1_envelope(self) -> None:
        bt.capture("checkout failed", level="error", module="payments",
                   data={"order": 42, "retry": True})
        self.assertTrue(bt.flush())
        events = self.collector.envelopes()
        self.assertEqual(len(events), 1)
        self.assertEqual(events[0]["v"], 1)
        event = events[0]["event"]
        self.assertEqual(event["app"], "unit-app")
        self.assertEqual(event["level"], "error")
        self.assertEqual(event["module"], "payments")
        self.assertEqual(event["data"], {"order": "42", "retry": "True"})
        self.assertTrue(event["timestamp"].endswith("Z"))

    def test_uncaught_exception_hook_is_installed(self) -> None:
        self.assertIsNot(sys.excepthook, sys.__excepthook__)
        self.assertIsNotNone(threading.excepthook)

    def test_logging_ships_errors_and_ignores_info(self) -> None:
        logger = logging.getLogger("unit.logger")
        logger.info("noise")
        logger.error("boom from logger")
        self.assertTrue(bt.flush())
        messages = [e["event"]["message"] for e in self.collector.envelopes()]
        self.assertIn("boom from logger", messages)
        self.assertNotIn("noise", messages)

    def test_capture_exception_keeps_the_real_traceback(self) -> None:
        try:
            raise ValueError("bad input")
        except ValueError as exc:
            bt.capture_exception(exc, order="7")
        self.assertTrue(bt.flush())
        event = self.collector.envelopes()[0]["event"]
        self.assertTrue(event["message"].startswith("ValueError: bad input"))
        self.assertTrue(any("ValueError" in line for line in event["backtrace"]))
        self.assertEqual(event["data"]["order"], "7")

    def test_release_marks_which_deploy_produced_the_event(self) -> None:
        env = bt.envelope("svc", "error", "boom", release="v9.2.1")
        self.assertEqual(env["event"]["release"], "v9.2.1")
        # Absent when unset: the wire stays compatible with older collectors.
        self.assertNotIn("release", bt.envelope("svc", "error", "boom")["event"])
        client = bt.Client(app="svc", release="v9.2.1")
        try:
            self.assertEqual(client.release, "v9.2.1")
        finally:
            client.shutdown(timeout=0.2)

    def test_failures_are_counted_and_never_raised(self) -> None:
        dead = bt.Client(url="http://127.0.0.1:1", app="dead", timeout=0.3)
        dead.send(message="nobody listens")
        dead.flush(3.0)
        self.assertGreaterEqual(dead.stats["failed"], 1)
        self.assertIsNotNone(dead.stats["last_error"])
        dead.shutdown()


class RecordingClient:
    def __init__(self) -> None:
        self.calls = []

    def send(self, **kwargs):
        self.calls.append(kwargs)
        return True


class _Request:
    def __init__(self, path="/boom", meta=None):
        self.method = "GET"
        self.path = path
        self.META = meta or {}


class _Response:
    def __init__(self, status):
        self.status_code = status
        self.headers = {}

    def __setitem__(self, key, value):
        self.headers[key] = value


class DjangoTest(unittest.TestCase):
    def test_5xx_is_captured_with_the_request_correlation(self) -> None:
        recorder = RecordingClient()
        middleware = TelemetryMiddleware(lambda request: _Response(500), client=recorder)
        response = middleware(_Request(meta={"HTTP_X_CORRELATION_ID": "req-123"}))
        self.assertEqual(response.headers["X-Correlation-Id"], "req-123")
        self.assertEqual(len(recorder.calls), 1)
        call = recorder.calls[0]
        self.assertEqual(call["correlation_id"], "req-123")
        self.assertEqual(call["data"]["status"], 500)
        self.assertIn("/boom", call["message"])

    def test_a_dropped_connection_is_retried_not_lost(self) -> None:
        collector = Collector(drops=1)
        client = bt.Client(url=collector.url, app="retry", timeout=2.0)
        try:
            client.send(level="error", message="kept", module="db")
            self.assertTrue(client.flush(5.0), "batch lost after a dropped connection")
            self.assertEqual(0, client.stats["failed"], "a missing answer must be retried")
            self.assertEqual(1, client.stats["sent"])
            self.assertEqual(
                ["kept"], [env["event"]["message"] for env in collector.envelopes()]
            )
        finally:
            client.shutdown()
            collector.stop()

    def test_2xx_is_not_captured_and_gets_an_id(self) -> None:
        recorder = RecordingClient()
        middleware = TelemetryMiddleware(lambda request: _Response(200), client=recorder)
        response = middleware(_Request())
        self.assertEqual(recorder.calls, [])
        self.assertTrue(response.headers["X-Correlation-Id"])


if __name__ == "__main__":
    unittest.main()
