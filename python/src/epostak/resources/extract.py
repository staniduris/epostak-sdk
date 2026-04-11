"""Extract resource -- AI-powered OCR extraction from PDFs and images.

Provides :class:`ExtractResource` for extracting structured invoice data from
uploaded files.  Requires the Enterprise plan.  Supported formats:
``application/pdf``, ``image/jpeg``, ``image/png``, ``image/webp`` (max 20 MB).
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional, Sequence, Tuple, Union, TYPE_CHECKING

if TYPE_CHECKING:
    from epostak.types import BatchExtractResult, ExtractResult

from epostak.resources.documents import _BaseResource


class ExtractResource(_BaseResource):
    """AI-powered OCR extraction from PDFs and images.

    Requires Enterprise plan. Supported MIME types: ``application/pdf``,
    ``image/jpeg``, ``image/png``, ``image/webp``. Max 20 MB per file.
    """

    def single(
        self,
        file: bytes,
        mime_type: str,
        file_name: str = "document",
    ) -> ExtractResult:
        """Extract structured data from a single file.

        Args:
            file: File content as raw bytes.
            mime_type: MIME type -- ``"application/pdf"``, ``"image/jpeg"``,
                ``"image/png"``, or ``"image/webp"``.
            file_name: Display name for the file (default ``"document"``).

        Returns:
            Dict with ``extraction`` (structured data), ``ubl_xml`` (generated UBL),
            ``confidence`` (0.0--1.0), and ``file_name``.

        Example::

            with open("invoice.pdf", "rb") as f:
                pdf_bytes = f.read()
            result = client.extract.single(pdf_bytes, "application/pdf", "invoice.pdf")
            print(result["confidence"], result["extraction"])
        """
        files = [("file", (file_name, file, mime_type))]
        return self._request("POST", "/extract", files=files)

    def batch(
        self,
        files: List[Dict[str, Any]],
    ) -> BatchExtractResult:
        """Extract structured data from multiple files (up to 10).

        Args:
            files: List of dicts, each with keys ``file`` (bytes), ``mime_type`` (str),
                and optional ``file_name`` (str, defaults to ``"document"``).

        Returns:
            Dict with ``batch_id``, ``total``, ``successful``, ``failed``, and ``results``
            (one entry per file with ``extraction``, ``ubl_xml``, ``confidence``, or ``error``).

        Example::

            result = client.extract.batch([
                {"file": pdf_bytes, "mime_type": "application/pdf", "file_name": "inv1.pdf"},
                {"file": img_bytes, "mime_type": "image/png", "file_name": "inv2.png"},
            ])
            print(f"{result['successful']}/{result['total']} successful")
        """
        upload_files: List[Tuple[str, Tuple[str, bytes, str]]] = []
        for f in files:
            name = f.get("file_name", "document")
            upload_files.append(("files", (name, f["file"], f["mime_type"])))
        return self._request("POST", "/extract/batch", files=upload_files)
