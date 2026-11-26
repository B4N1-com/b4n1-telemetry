"""Correlation context and access to the installed client.

The correlation id lives in a ``ContextVar``, not a global: one request's id
must never appear on another request's events, and many Python deployments are
threaded.
"""

from __future__ import annotations

from contextvars import ContextVar
from typing import Any, Dict, Optional

from .client import Client

_correlation: ContextVar[Optional[str]] = ContextVar("b4n1_correlation", default=None)

_installed: Dict[str, Any] = {
    "client": None,
    "handler": None,
    "excepthook": None,
    "thread_hook": None,
}


def client_or_raise() -> Client:
    client = _installed["client"]
    if client is None:
        raise RuntimeError(
            "b4n1_telemetry.init(app=...) must be called before capture()"
        )
    return client


def current_client() -> Optional[Client]:
    return _installed["client"]


def correlation_id() -> Optional[str]:
    return _correlation.get()


def set_correlation(value: Optional[str]) -> Any:
    return _correlation.set(value)


def reset_correlation(token: Any) -> None:
    _correlation.reset(token)
