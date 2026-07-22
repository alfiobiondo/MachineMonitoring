from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from typing import Any


JsonObject = dict[str, Any]


class ApiError(RuntimeError):
    def __init__(
        self,
        *,
        method: str,
        url: str,
        status: int | None,
        title: str,
        detail: str | None = None,
        trace_id: str | None = None,
    ) -> None:
        self.method = method
        self.url = url
        self.status = status
        self.title = title
        self.detail = detail
        self.trace_id = trace_id
        super().__init__(self._format_message())

    def _format_message(self) -> str:
        status = "network error" if self.status is None else f"HTTP {self.status}"
        parts = [f"{status} during {self.method} {self.url}", f"title: {self.title}"]
        if self.detail:
            parts.append(f"detail: {self.detail}")
        if self.trace_id:
            parts.append(f"traceId: {self.trace_id}")
        return "\n".join(parts)


@dataclass(frozen=True)
class ApiClient:
    base_url: str = "http://localhost:5221"
    timeout_seconds: float = 20

    def get_json(
        self,
        path: str,
        query: JsonObject | None = None,
    ) -> Any:
        return self._request_json("GET", path, query=query)

    def post_json(
        self,
        path: str,
        payload: JsonObject | None = None,
    ) -> Any:
        return self._request_json("POST", path, payload=payload)

    def _request_json(
        self,
        method: str,
        path: str,
        *,
        payload: JsonObject | None = None,
        query: JsonObject | None = None,
    ) -> Any:
        url = self._build_url(path, query)
        data = None
        headers = {"Accept": "application/json"}

        if payload is not None:
            data = json.dumps(payload).encode("utf-8")
            headers["Content-Type"] = "application/json"

        request = urllib.request.Request(url, data=data, headers=headers, method=method)

        try:
            with urllib.request.urlopen(request, timeout=self.timeout_seconds) as response:
                return _parse_response_body(response.read(), response.headers.get("Content-Type"))
        except urllib.error.HTTPError as error:
            body = error.read()
            title, detail, trace_id = _parse_problem_details(
                body,
                error.headers.get("Content-Type"),
                fallback_title=error.reason,
            )
            raise ApiError(
                method=method,
                url=url,
                status=error.code,
                title=title,
                detail=detail,
                trace_id=trace_id,
            ) from error
        except urllib.error.URLError as error:
            raise ApiError(
                method=method,
                url=url,
                status=None,
                title="API non raggiungibile",
                detail=str(error.reason),
                trace_id=None,
            ) from error

    def _build_url(self, path: str, query: JsonObject | None) -> str:
        base = self.base_url.rstrip("/")
        normalized_path = path if path.startswith("/") else f"/{path}"
        url = f"{base}{normalized_path}"

        if not query:
            return url

        encoded_query = urllib.parse.urlencode(
            {key: value for key, value in query.items() if value is not None}
        )
        return f"{url}?{encoded_query}" if encoded_query else url


def _parse_response_body(body: bytes, content_type: str | None) -> Any:
    if not body:
        return None

    decoded = body.decode("utf-8")
    if _is_json_content(content_type):
        return json.loads(decoded)

    return decoded


def _parse_problem_details(
    body: bytes,
    content_type: str | None,
    *,
    fallback_title: str,
) -> tuple[str, str | None, str | None]:
    if not body:
        return fallback_title, None, None

    decoded = body.decode("utf-8", errors="replace")
    if not _is_json_content(content_type):
        return fallback_title, decoded, None

    try:
        payload = json.loads(decoded)
    except json.JSONDecodeError:
        return fallback_title, decoded, None

    if not isinstance(payload, dict):
        return fallback_title, decoded, None

    title = str(payload.get("title") or fallback_title)
    detail = payload.get("detail")
    trace_id = payload.get("traceId") or payload.get("trace_id")

    return (
        title,
        None if detail is None else str(detail),
        None if trace_id is None else str(trace_id),
    )


def _is_json_content(content_type: str | None) -> bool:
    if content_type is None:
        return False

    media_type = content_type.split(";", 1)[0].strip().lower()
    return media_type in {"application/json", "application/problem+json"} or media_type.endswith(
        "+json"
    )
