import { BaseResource } from "../utils/request.js";
import type { ExtractResult, BatchExtractResult } from "../types.js";

export class ExtractResource extends BaseResource {
  single(file: Buffer | Blob, mimeType: string, fileName = "document"): Promise<ExtractResult> {
    const form = new FormData();
    const blob = file instanceof Blob ? file : new Blob([new Uint8Array(file)], { type: mimeType });
    form.append("file", blob, fileName);
    return this.request("POST", "/extract", form);
  }

  batch(files: Array<{ file: Buffer | Blob; mimeType: string; fileName?: string }>): Promise<BatchExtractResult> {
    const form = new FormData();
    for (const { file, mimeType, fileName } of files) {
      const blob = file instanceof Blob ? file : new Blob([new Uint8Array(file)], { type: mimeType });
      form.append("files", blob, fileName ?? "document");
    }
    return this.request("POST", "/extract/batch", form);
  }
}
