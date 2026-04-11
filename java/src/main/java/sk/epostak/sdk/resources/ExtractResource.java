package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.BatchExtractResult;
import sk.epostak.sdk.models.ExtractResult;

import java.util.List;

/**
 * AI-powered OCR extraction from PDFs and images.
 * <p>
 * Extracts structured invoice data from uploaded files using AI vision models.
 * Supports PDF, JPEG, PNG, and WebP formats. Returns extracted fields, a
 * generated UBL XML, and a confidence score.
 * <p>
 * Access via {@code client.extract()}.
 *
 * <pre>{@code
 * byte[] pdfBytes = Files.readAllBytes(Path.of("invoice.pdf"));
 * ExtractResult result = client.extract().single(pdfBytes, "invoice.pdf", "application/pdf");
 * System.out.println("Confidence: " + result.confidence());
 * System.out.println("UBL: " + result.ublXml());
 * }</pre>
 */
public final class ExtractResource {

    private final HttpClient http;

    /**
     * Creates a new extract resource.
     *
     * @param http the HTTP client used for API communication
     */
    public ExtractResource(HttpClient http) {
        this.http = http;
    }

    /**
     * Extract structured invoice data from a single file.
     *
     * @param fileBytes raw file content as a byte array
     * @param fileName  the file name with extension, e.g. {@code "invoice.pdf"}
     * @param mimeType  MIME type of the file: {@code "application/pdf"}, {@code "image/jpeg"},
     *                  {@code "image/png"}, or {@code "image/webp"}
     * @return the extraction result with parsed fields, UBL XML, and confidence score
     * @throws sk.epostak.sdk.EPostakException if the file type is unsupported or the request fails
     */
    public ExtractResult single(byte[] fileBytes, String fileName, String mimeType) {
        return http.postMultipart("/extract", fileBytes, fileName, mimeType, ExtractResult.class);
    }

    /**
     * Extract structured invoice data from multiple files in a single request (max 10).
     *
     * @param files list of file inputs to extract (max 10)
     * @return the batch extraction result with per-file results
     * @throws sk.epostak.sdk.EPostakException if the request fails or any file type is unsupported
     */
    public BatchExtractResult batch(List<FileInput> files) {
        List<HttpClient.FileUpload> uploads = files.stream()
                .map(f -> new HttpClient.FileUpload(f.data(), f.fileName(), f.mimeType()))
                .toList();
        return http.postMultipartBatch("/extract/batch", uploads, BatchExtractResult.class);
    }

    /**
     * Input for a file to extract.
     *
     * @param data     raw file content as a byte array
     * @param fileName the file name with extension, e.g. {@code "invoice.pdf"}
     * @param mimeType MIME type: {@code "application/pdf"}, {@code "image/jpeg"},
     *                 {@code "image/png"}, or {@code "image/webp"}
     */
    public record FileInput(byte[] data, String fileName, String mimeType) {}
}
