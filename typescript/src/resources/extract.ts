import { BaseResource } from "../utils/request.js";
import type { ExtractResult, BatchExtractResult } from "../types.js";

/**
 * Resource for AI-powered data extraction from PDF invoices and scanned images.
 * Extracts structured invoice data (parties, line items, totals) and generates
 * UBL 2.1 XML that can be sent via Peppol.
 *
 * @example
 * ```typescript
 * import { readFileSync } from 'fs';
 *
 * const pdf = readFileSync('invoice.pdf');
 * const result = await client.extract.single(pdf, 'application/pdf', 'invoice.pdf');
 * console.log(result.confidence); // 0.95
 * console.log(result.ubl_xml); // Generated UBL XML
 * ```
 */
export class ExtractResource extends BaseResource {
  /**
   * Extract structured data from a single PDF or image file.
   * Uses AI-powered OCR to identify invoice fields, line items, parties, and totals,
   * then generates UBL 2.1 XML from the extracted data.
   *
   * @param file - File content as a Buffer or Blob
   * @param mimeType - MIME type of the file (e.g. `"application/pdf"`, `"image/png"`)
   * @param fileName - Original file name for reference (defaults to `"document"`)
   * @returns Extracted data, generated UBL XML, and confidence score
   *
   * @example
   * ```typescript
   * import { readFileSync } from 'fs';
   *
   * const pdf = readFileSync('invoice.pdf');
   * const result = await client.extract.single(pdf, 'application/pdf', 'invoice.pdf');
   * if (result.confidence === 'high') {
   *   // Use the generated UBL to send via Peppol
   *   await client.documents.send({
   *     receiverPeppolId: '0245:1234567890',
   *     xml: result.ubl_xml,
   *   });
   * }
   * ```
   */
  single(
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
    return this.request("POST", "/extract", form);
  }

  /**
   * Extract structured data from multiple PDF or image files in a single request.
   * Each file is processed independently — individual failures don't affect others.
   *
   * @param files - Array of files with their MIME types and optional file names
   * @returns Batch result with individual outcomes per file and success/failure counts
   *
   * @example
   * ```typescript
   * import { readFileSync } from 'fs';
   *
   * const result = await client.extract.batch([
   *   { file: readFileSync('invoice1.pdf'), mimeType: 'application/pdf', fileName: 'invoice1.pdf' },
   *   { file: readFileSync('invoice2.pdf'), mimeType: 'application/pdf', fileName: 'invoice2.pdf' },
   * ]);
   * console.log(`${result.successful}/${result.total} extracted successfully`);
   * ```
   */
  batch(
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
    return this.request("POST", "/extract/batch", form);
  }
}
