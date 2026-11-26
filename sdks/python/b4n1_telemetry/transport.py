"""The POST itself: the only module that knows HTTP exists.

A missing answer is not a refusal. A timeout against a healthy collector means
the collector is fsyncing, and dropping the batch there is silent loss, so the
request is attempted once more. Any HTTP answer is returned to the caller
untouched: a 4xx would repeat the same mistake on a retry, and a 429 is the
collector asking for time.
"""

from __future__ import annotations

import json
import time
from typing import Any, Dict, List, Optional, Tuple
from urllib import request as urlrequest

RETRY_PAUSE_SECONDS = 0.1

Content = List[Dict[str, Any]]


def post(
    url: str, token: Optional[str], batch: Content, timeout: float
) -> Tuple[Optional[int], Optional[str]]:
    """POST one batch.

    Returns ``(status, error)``: exactly one is ``None``. ``status`` is the HTTP
    code the collector answered with; ``error`` means no answer arrived.
    """
    body = json.dumps(batch).encode("utf-8")
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    request = urlrequest.Request(
        url + "/api/v1/events", data=body, headers=headers, method="POST"
    )
    failure: Optional[Exception] = None
    for attempt in (1, 2):
        try:
            with urlrequest.urlopen(request, timeout=timeout) as response:
                response.read()
                return response.getcode(), None
        except OSError as exc:
            # An HTTP error status is an answer, not a transport failure.
            code = getattr(exc, "code", None)
            if code is not None:
                return int(code), None
            failure = exc
            if attempt == 1:
                time.sleep(RETRY_PAUSE_SECONDS)
    return None, "%s: %s" % (type(failure).__name__, failure)
