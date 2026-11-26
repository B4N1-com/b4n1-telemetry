"""Django middleware: one correlation id per request, 5xx captured.

Add to ``MIDDLEWARE`` after ``b4n1_telemetry.init(app="<app>")``::

    "b4n1_telemetry.django.TelemetryMiddleware",

Requests arriving with ``X-Correlation-Id`` keep it; everything the request
triggers (logs, exceptions) is stamped with the same id, so the whole chain is
one ``trace`` lookup. Uncaught exceptions are captured by the logging handler
Django itself uses, so this middleware does not duplicate them.
"""

from __future__ import annotations

import uuid
from typing import Any, Optional

from .auto import client_or_raise, reset_correlation, set_correlation

HEADER = "X-Correlation-Id"
HEADER_ALIASES = ("HTTP_X_CORRELATION_ID", "HTTP_X_REQUEST_ID")


def correlation_for(request: Any) -> str:
    meta = getattr(request, "META", None) or {}
    for key in HEADER_ALIASES:
        value = meta.get(key)
        if value:
            return str(value)
    headers = getattr(request, "headers", None)
    if headers is not None:
        try:
            value = headers.get(HEADER)
        except Exception:
            value = None
        if value:
            return str(value)
    return uuid.uuid4().hex


class TelemetryMiddleware:
    """Correlation + 5xx reporting. Never swallows an exception."""

    def __init__(self, get_response: Any, client: Optional[Any] = None) -> None:
        self.get_response = get_response
        self.client = client or client_or_raise()

    def __call__(self, request: Any) -> Any:
        correlation = correlation_for(request)
        token = set_correlation(correlation)
        try:
            response = self.get_response(request)
            status = int(getattr(response, "status_code", 0) or 0)
            if status >= 500:
                self._capture(request, status, correlation)
            try:
                response[HEADER] = correlation
            except Exception:
                pass
            return response
        finally:
            reset_correlation(token)

    def _capture(self, request: Any, status: int, correlation: str) -> None:
        method = getattr(request, "method", "?")
        path = getattr(request, "path", "?")
        self.client.send(
            level="error",
            message="HTTP %s %s %s" % (status, method, path),
            module="django.request",
            data={"status": status, "method": method, "path": path},
            correlation_id=correlation,
        )
