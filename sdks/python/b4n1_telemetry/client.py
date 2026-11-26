"""Transport and envelope construction. Standard library only.

The SDK must never break the application that embeds it: send failures are
counted and exposed through ``Client.stats``, never raised into the host.
"""

from __future__ import annotations

import os
import queue
import threading
import time
from datetime import datetime, timezone
from typing import Any, Callable, Dict, List, Optional
from . import transport

ENVELOPE_VERSION = 1
DEFAULT_URL = "http://127.0.0.1:9450"


def now_iso() -> str:
    """UTC timestamp in the RFC 3339 form the collector parses."""
    return (
        datetime.now(timezone.utc)
        .isoformat(timespec="milliseconds")
        .replace("+00:00", "Z")
    )


def envelope(
    app: str,
    level: str,
    message: str,
    module: str = "",
    data: Optional[Dict[str, Any]] = None,
    backtrace: Optional[List[str]] = None,
    correlation_id: Optional[str] = None,
    breadcrumbs: Optional[List[Dict[str, str]]] = None,
    timestamp: Optional[str] = None,
    release: Optional[str] = None,
) -> Dict[str, Any]:
    """Build one wire envelope (``{"v":1,"event":{...}}``).

    ``release`` is the deploy identifier (git sha, version, image tag). It is
    omitted when unset so the wire stays compatible with older collectors.
    """
    body: Dict[str, Any] = {
        "v": ENVELOPE_VERSION,
        "event": {
            "timestamp": timestamp or now_iso(),
            "app": app,
            "level": level,
            "message": message,
            "module": module,
            "data": {str(k): str(v) for k, v in (data or {}).items()},
            "backtrace": list(backtrace or []),
            "correlation_id": correlation_id,
            "breadcrumbs": list(breadcrumbs or []),
        },
    }
    if release:
        body["event"]["release"] = release
    return body


class Client:
    """Bounded queue + background sender.

    Sending is asynchronous and best-effort by design: an observability client
    that blocks or raises inside the application turns a bug report into an
    outage. ``stats`` is the honest ledger of what was dropped or rejected.
    """

    def __init__(
        self,
        url: Optional[str] = None,
        token: Optional[str] = None,
        app: str = "python",
        timeout: float = 3.0,
        queue_size: int = 2048,
        on_error: Optional[Callable[[str], None]] = None,
        release: Optional[str] = None,
    ) -> None:
        self.url = (url or DEFAULT_URL).rstrip("/")
        self.token = token
        self.app = app
        # Release comes from the caller or the deploy environment; both are
        # normal for a container that exports the image tag.
        self.release = release or os.environ.get("B4N1_TELEMETRY_RELEASE") or None
        self.timeout = timeout
        self._queue: "queue.Queue[Optional[Dict[str, Any]]]" = queue.Queue(queue_size)
        self._stop = threading.Event()
        self._dropped = 0
        self._failed = 0
        self._sent = 0
        self._last_error: Optional[str] = None
        self._on_error = on_error
        self._worker = threading.Thread(
            target=self._run, name="b4n1-telemetry", daemon=True
        )
        self._worker.start()

    def send(
        self,
        level: str = "error",
        message: str = "",
        module: str = "",
        data: Optional[Dict[str, Any]] = None,
        backtrace: Optional[List[str]] = None,
        correlation_id: Optional[str] = None,
        app: Optional[str] = None,
    ) -> bool:
        """Enqueue one event. Returns False when the queue is full."""
        return self.send_envelope(
            envelope(
                app or self.app,
                level,
                message,
                module=module,
                data=data,
                backtrace=backtrace,
                correlation_id=correlation_id,
                release=self.release,
            )
        )

    def send_envelope(self, env: Dict[str, Any]) -> bool:
        try:
            self._queue.put_nowait(env)
            return True
        except queue.Full:
            self._dropped += 1
            return False

    def flush(self, timeout: float = 2.0) -> bool:
        """Block until the queue drains or ``timeout`` expires."""
        deadline = time.monotonic() + timeout
        while self._queue.unfinished_tasks and time.monotonic() < deadline:
            time.sleep(0.005)
        return self._queue.unfinished_tasks == 0

    def shutdown(self, timeout: float = 2.0) -> None:
        self.flush(timeout)
        self._stop.set()
        try:
            self._queue.put_nowait(None)
        except queue.Full:
            pass
        self._worker.join(timeout)

    @property
    def stats(self) -> Dict[str, Any]:
        return {
            "url": self.url,
            "sent": self._sent,
            "failed": self._failed,
            "dropped": self._dropped,
            "last_error": self._last_error,
        }

    def _run(self) -> None:
        while True:
            try:
                env = self._queue.get(timeout=0.2)
            except queue.Empty:
                if self._stop.is_set():
                    return
                continue
            try:
                if env is None:
                    return
                self._post(env)
            finally:
                self._queue.task_done()

    def _post(self, env: Dict[str, Any]) -> None:
        status, error = transport.post(self.url, self.token, [env], self.timeout)
        if error is None and status is not None and 200 <= status < 300:
            self._sent += 1
            return
        # Never propagated into the host application.
        self._failed += 1
        self._last_error = error or "HTTP %d" % status
        if self._on_error is not None:
            self._on_error(self._last_error)
