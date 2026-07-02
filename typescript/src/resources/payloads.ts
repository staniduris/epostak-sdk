import { BaseResource } from "../utils/request.js";
import type { BatchExtractResult, ExtractResult } from "../types.js";
import type {
  ConvertRequest,
  ConvertResult,
  ParsedUblDocument,
  ValidateDocumentRequest,
  ValidationResult,
} from "../types.js";

/**
 * Payload Assistant helpers for creating, parsing, converting, and validating
 * send payloads before dispatch.
 */
export class PayloadsResource extends BaseResource {
  extract(
    file: Buffer | Blob,
    mimeType: string,
    fileName = "document",
  ): Promise<ExtractResult> {
    const form = new FormData();
    const blob =
      file instanceof Blob
        ? file
        : new Blob([new Uint8Array(file)], { type: mimeType });
    form.append("file", blob, fileName);
    return this.request("POST", "/payloads/extract", form);
  }

  extractBatch(
    files: Array<{ file: Buffer | Blob; mimeType: string; fileName?: string }>,
  ): Promise<BatchExtractResult> {
    const form = new FormData();
    for (const { file, mimeType, fileName } of files) {
      const blob =
        file instanceof Blob
          ? file
          : new Blob([new Uint8Array(file)], { type: mimeType });
      form.append("files", blob, fileName ?? "document");
    }
    return this.request("POST", "/payloads/extract/batch", form);
  }

  parse(xml: string): Promise<ParsedUblDocument> {
    return this.request("POST", "/payloads/parse", { xml });
  }

  convert(body: ConvertRequest): Promise<ConvertResult> {
    return this.request("POST", "/payloads/convert", body);
  }

  validate(body: ValidateDocumentRequest): Promise<ValidationResult> {
    return this.request("POST", "/payloads/validate", body);
  }
}
