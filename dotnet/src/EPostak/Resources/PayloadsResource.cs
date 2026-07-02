using System.Net.Http.Headers;
using EPostak.Models;

namespace EPostak.Resources;

/// <summary>Payload Assistant helpers for OCR, parse, convert, and validation.</summary>
public sealed class PayloadsResource
{
    private readonly HttpRequestor _http;

    internal PayloadsResource(HttpRequestor http) => _http = http;

    public Task<ExtractResult> ExtractAsync(Stream stream, string mimeType, string? fileName = null, CancellationToken ct = default)
    {
        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        content.Add(streamContent, "file", fileName ?? "document");
        return _http.RequestMultipartAsync<ExtractResult>(HttpMethod.Post, "/payloads/extract", content, ct);
    }

    public Task<BatchExtractResult> ExtractBatchAsync(IEnumerable<ExtractFile> files, CancellationToken ct = default)
    {
        var content = new MultipartFormDataContent();
        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.Stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.MimeType);
            content.Add(streamContent, "files", file.FileName ?? "document");
        }
        return _http.RequestMultipartAsync<BatchExtractResult>(HttpMethod.Post, "/payloads/extract/batch", content, ct);
    }

    public Task<ParsedInvoice> ParseAsync(string xml, CancellationToken ct = default)
        => _http.RequestAsync<ParsedInvoice>(HttpMethod.Post, "/payloads/parse", new { xml }, ct);

    public Task<ConvertResult> ConvertAsync(ConvertRequest request, CancellationToken ct = default)
        => _http.RequestAsync<ConvertResult>(HttpMethod.Post, "/payloads/convert", request, ct);

    public Task<ValidationResult> ValidateAsync(object request, CancellationToken ct = default)
        => _http.RequestAsync<ValidationResult>(HttpMethod.Post, "/payloads/validate", request, ct);
}
