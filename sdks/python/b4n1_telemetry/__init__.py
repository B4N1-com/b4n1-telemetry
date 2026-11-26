"""b4n1-telemetry Python SDK — drop-in error reporting to your own collector.

Three lines in a Django project::

    import b4n1_telemetry
    b4n1_telemetry.init(app="musicogram")
    # MIDDLEWARE += ["b4n1_telemetry.django.TelemetryMiddleware"]

After that, every ERROR log and every uncaught exception (main thread and
worker threads) is recorded, correlated per request, with no per-call-site
code. Standard library only: the SDK adds no dependency to the application.

Environment: ``B4N1_TELEMETRY_URL``, ``B4N1_TELEMETRY_TOKEN``,
``B4N1_TELEMETRY_APP``.
"""

from .auto import (
    capture,
    capture_exception,
    correlation_id,
    current_client,
    flush,
    init,
    init_from_env,
    shutdown,
)
from .client import Client, envelope, now_iso

__all__ = [
    "Client",
    "capture",
    "capture_exception",
    "correlation_id",
    "current_client",
    "envelope",
    "flush",
    "init",
    "init_from_env",
    "now_iso",
    "shutdown",
]
__version__ = "0.1.1"
