"""Log-level mapping and traceback formatting.

Split out of ``auto`` so the integration module describes how events are
captured, not how a Python log level or an ``exc_info`` triple is spelled.
"""

from __future__ import annotations

import logging
import traceback
from typing import Any, List, Tuple

# Highest first: the first threshold at or below the record level wins, so an
# unknown level still maps to something the collector accepts.
_LEVELS: Tuple[Tuple[int, str], ...] = (
    (logging.CRITICAL, "fatal"),
    (logging.ERROR, "error"),
    (logging.WARNING, "warning"),
    (logging.INFO, "info"),
    (logging.DEBUG, "debug"),
)


def level_name(levelno: int) -> str:
    """Map a ``logging`` level number to the collector's level names."""
    for threshold, name in _LEVELS:
        if levelno >= threshold:
            return name
    return "debug"


def frames_from(exc_info: Any) -> List[str]:
    """Format an ``exc_info`` triple as one string per frame line."""
    if not exc_info or exc_info[0] is None:
        return []
    return traceback.format_exception(*exc_info)


def frames_of(exc: BaseException) -> List[str]:
    """Format a caught exception with its real traceback."""
    return traceback.format_exception(type(exc), exc, exc.__traceback__)
