"""Drop-in instrumentation: logging handler + uncaught exception hooks.

``b4n1_telemetry.init(app="musicogram")`` is the whole integration. Every
ERROR-level log and every uncaught exception (main thread and threads) becomes
an event, already correlated, with no per-call-site code.
"""

from __future__ import annotations

import atexit
import logging
import os
import sys
import threading
import traceback
from typing import Any, Dict, List, Optional

from .client import Client

from .context import (
    _correlation,
    _installed,
    client_or_raise,
    correlation_id,
    current_client,
    reset_correlation,
    set_correlation,
)
from .levels import frames_from, frames_of, level_name


class TelemetryHandler(logging.Handler):
    """Ships log records at or above ``level``; never raises into the app."""

    def __init__(self, client: Client, app: str, level: int = logging.ERROR) -> None:
        super().__init__(level=level)
        self.client = client
        self.app = app

    def emit(self, record: logging.LogRecord) -> None:
        try:
            backtrace = frames_from(record.exc_info)
            self.client.send(
                level=level_name(record.levelno),
                message=record.getMessage(),
                module=record.name or record.module,
                data={
                    "file": record.pathname,
                    "line": record.lineno,
                    "logger": record.name,
                },
                backtrace=backtrace,
                correlation_id=_correlation.get(),
            )
        except Exception:  # a logging handler must never break logging
            pass


def init(
    app: Optional[str] = None,
    url: Optional[str] = None,
    token: Optional[str] = None,
    level: int = logging.ERROR,
    capture_uncaught: bool = True,
    client: Optional[Client] = None,
    release: Optional[str] = None,
) -> Client:
    """Install the handler + hooks and return the client (idempotent).

    ``release`` marks which deploy produced the events; it falls back to
    ``B4N1_TELEMETRY_RELEASE`` so a container can wire it once.
    """
    if _installed["client"] is not None:
        return _installed["client"]

    app = app or os.environ.get("B4N1_TELEMETRY_APP") or "python"
    url = url or os.environ.get("B4N1_TELEMETRY_URL")
    token = token or os.environ.get("B4N1_TELEMETRY_TOKEN")
    client = client or Client(url=url, token=token, app=app, release=release)

    handler = TelemetryHandler(client, app, level=level)
    logging.getLogger().addHandler(handler)

    if capture_uncaught:
        previous = sys.excepthook

        def hook(exc_type, exc, tb):  # noqa: ANN001 - sys.excepthook signature
            client.send(
                level="fatal",
                message="%s: %s" % (exc_type.__name__, exc),
                module="uncaught",
                data={"thread": "main"},
                backtrace=traceback.format_exception(exc_type, exc, tb),
                correlation_id=_correlation.get(),
            )
            previous(exc_type, exc, tb)

        sys.excepthook = hook
        _installed["excepthook"] = previous

        previous_thread = threading.excepthook

        def thread_hook(args):  # noqa: ANN001 - threading.excepthook signature
            client.send(
                level="fatal",
                message="%s: %s" % (args.exc_type.__name__, args.exc_value),
                module="uncaught",
                data={"thread": getattr(args.thread, "name", "worker")},
                backtrace=traceback.format_exception(
                    args.exc_type, args.exc_value, args.exc_traceback
                ),
                correlation_id=_correlation.get(),
            )
            previous_thread(args)

        threading.excepthook = thread_hook
        _installed["thread_hook"] = previous_thread

    atexit.register(client.shutdown)
    _installed.update(client=client, handler=handler)
    return client


init_from_env = init


def shutdown() -> None:
    """Remove the handler and restore the previous hooks (tests, reloads)."""
    client = _installed["client"]
    if client is None:
        return
    handler = _installed["handler"]
    if handler is not None:
        logging.getLogger().removeHandler(handler)
    if _installed["excepthook"] is not None:
        sys.excepthook = _installed["excepthook"]
    if _installed["thread_hook"] is not None:
        threading.excepthook = _installed["thread_hook"]
    client.shutdown()
    _installed.update(client=None, handler=None, excepthook=None, thread_hook=None)


def capture(
    message: str,
    level: str = "error",
    module: str = "",
    data: Optional[Dict[str, Any]] = None,
    correlation_id: Optional[str] = None,
    backtrace: Optional[List[str]] = None,
) -> None:
    """Record one event explicitly."""
    client_or_raise().send(
        level=level,
        message=message,
        module=module,
        data=data,
        backtrace=backtrace,
        correlation_id=correlation_id or _correlation.get(),
    )


def capture_exception(exc: BaseException, module: str = "", **extra: Any) -> None:
    """Record a caught exception with its real traceback."""
    client_or_raise().send(
        level="error",
        message="%s: %s" % (type(exc).__name__, exc),
        module=module or type(exc).__module__,
        data=extra,
        backtrace=frames_of(exc),
        correlation_id=_correlation.get(),
    )


def flush(timeout: float = 2.0) -> bool:
    client = _installed["client"]
    return True if client is None else client.flush(timeout)
