"""Payload Assistant resource."""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from epostak.resources.documents import _BaseResource


class PayloadsResource(_BaseResource):
    """Create, parse, convert, and validate send payloads."""

    def extract(
        self,
        file: bytes,
        mime_type: str,
        file_name: str = "document",
    ) -> Dict[str, Any]:
        files = [("file", (file_name, file, mime_type))]
        return self._request("POST", "/payloads/extract", files=files)

    def extract_batch(self, files: List[Dict[str, Any]]) -> Dict[str, Any]:
        upload_files = []
        for item in files:
            upload_files.append(
                ("files", (item.get("file_name", "document"), item["file"], item["mime_type"]))
            )
        return self._request("POST", "/payloads/extract/batch", files=upload_files)

    def parse(self, xml: str) -> Dict[str, Any]:
        return self._request("POST", "/payloads/parse", json={"xml": xml})

    def convert(self, input_format: str, output_format: str, document: Any) -> Dict[str, Any]:
        return self._request(
            "POST",
            "/payloads/convert",
            json={
                "input_format": input_format,
                "output_format": output_format,
                "document": document,
            },
        )

    def validate(self, body: Dict[str, Any]) -> Dict[str, Any]:
        return self._request("POST", "/payloads/validate", json=body)
