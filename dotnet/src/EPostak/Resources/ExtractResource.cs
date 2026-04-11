using System.Net.Http.Headers;
using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// AI-powered OCR extraction from PDFs and images. Extracts structured invoice data
/// (supplier, customer, line items, totals) and generates UBL XML from scanned or
/// photographed documents. Supports PDF, PNG, JPEG, and TIFF formats.
/// </summary>
public sealed class ExtractResource
{
    private readonly HttpRequestor _http;

    internal ExtractResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// Extract structured invoice data from a single file (PDF or image).
    /// The AI model reads the document, extracts supplier/customer details, line items,
    /// and totals, then generates a UBL XML representation ready for Peppol transmission.
    /// </summary>
    /// <param name="stream">The file content stream (PDF, PNG, JPEG, or TIFF).</param>
    /// <param name="mimeType">The MIME type of the file (e.g. "application/pdf", "image/png").</param>
    /// <param name="fileName">Optional file name for logging and result identification.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Extracted structured data, generated UBL XML, confidence score, and file name.</returns>
    /// <example>
    /// <code>
    /// using var fileStream = File.OpenRead("invoice_scan.pdf");
    /// var result = await client.Extract.SingleAsync(fileStream, "application/pdf", "invoice_scan.pdf");
    /// Console.WriteLine($"Confidence: {result.Confidence:P0}");
    /// Console.WriteLine($"UBL XML length: {result.UblXml.Length} chars");
    /// </code>
    /// </example>
    public Task<ExtractResult> SingleAsync(Stream stream, string mimeType, string? fileName = null, CancellationToken ct = default)
    {
        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        content.Add(streamContent, "file", fileName ?? "document");
        return _http.RequestMultipartAsync<ExtractResult>(HttpMethod.Post, "/extract", content, ct);
    }

    /// <summary>
    /// Extract structured invoice data from multiple files in a single batch request.
    /// Each file is processed independently -- partial failures don't block other extractions.
    /// </summary>
    /// <param name="files">Collection of files to extract, each with a stream, MIME type, and optional file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch results with per-file extraction data, success/failure counts, and a batch ID.</returns>
    /// <example>
    /// <code>
    /// var files = new List&lt;ExtractFile&gt;
    /// {
    ///     new() { Stream = File.OpenRead("inv1.pdf"), MimeType = "application/pdf", FileName = "inv1.pdf" },
    ///     new() { Stream = File.OpenRead("inv2.png"), MimeType = "image/png", FileName = "inv2.png" }
    /// };
    /// var result = await client.Extract.BatchAsync(files);
    /// Console.WriteLine($"Batch {result.BatchId}: {result.Successful}/{result.Total} successful");
    /// </code>
    /// </example>
    public Task<BatchExtractResult> BatchAsync(IEnumerable<ExtractFile> files, CancellationToken ct = default)
    {
        var content = new MultipartFormDataContent();
        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.Stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.MimeType);
            content.Add(streamContent, "files", file.FileName ?? "document");
        }
        return _http.RequestMultipartAsync<BatchExtractResult>(HttpMethod.Post, "/extract/batch", content, ct);
    }
}
