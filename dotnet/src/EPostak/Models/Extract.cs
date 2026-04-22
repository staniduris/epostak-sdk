using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Extract
// ---------------------------------------------------------------------------

/// <summary>
/// Result of AI-powered OCR extraction from a single document.
/// Contains the structured extracted data, generated UBL XML, per-field
/// confidence scores, and a coarse overall confidence level.
/// </summary>
public sealed class ExtractResult
{
    /// <summary>Structured extraction data (supplier, customer, line items, totals, etc.) as key-value pairs.</summary>
    [JsonPropertyName("extraction")]
    public Dictionary<string, object> Extraction { get; set; } = [];

    /// <summary>UBL 2.1 XML generated from the extracted data, ready for Peppol transmission.</summary>
    [JsonPropertyName("ubl_xml")]
    public string UblXml { get; set; } = "";

    /// <summary>Overall confidence level: <c>high</c>, <c>medium</c>, or <c>low</c>.</summary>
    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = "";

    /// <summary>Per-field numeric confidence scores (0.0 – 1.0) keyed by field name (e.g. <c>vendor_ico</c>, <c>total</c>).</summary>
    [JsonPropertyName("confidence_scores")]
    public Dictionary<string, double> ConfidenceScores { get; set; } = [];

    /// <summary>True when the extraction should be manually reviewed (overall confidence is <c>medium</c> or <c>low</c>).</summary>
    [JsonPropertyName("needs_review")]
    public bool NeedsReview { get; set; }

    /// <summary>Name of the file that was processed.</summary>
    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = "";
}

/// <summary>
/// Extraction result for a single file within a batch extraction request.
/// Contains either a successful extraction or an error message.
/// </summary>
public sealed class BatchExtractItem
{
    /// <summary>Name of the file that was processed.</summary>
    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = "";

    /// <summary>Structured extraction data. Null if extraction failed.</summary>
    [JsonPropertyName("extraction")]
    public Dictionary<string, object>? Extraction { get; set; }

    /// <summary>Generated UBL XML. Null if extraction failed.</summary>
    [JsonPropertyName("ubl_xml")]
    public string? UblXml { get; set; }

    /// <summary>AI confidence score as a string. Null if extraction failed.</summary>
    [JsonPropertyName("confidence")]
    public string? Confidence { get; set; }

    /// <summary>Error message if extraction failed for this file. Null on success.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Result of a batch extraction request containing individual results for each file.
/// </summary>
public sealed class BatchExtractResult
{
    /// <summary>Unique batch identifier for tracking this extraction job.</summary>
    [JsonPropertyName("batch_id")]
    public string BatchId { get; set; } = "";

    /// <summary>Total number of files submitted in the batch.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Number of files successfully extracted.</summary>
    [JsonPropertyName("successful")]
    public int Successful { get; set; }

    /// <summary>Number of files that failed extraction.</summary>
    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    /// <summary>Individual extraction results for each file in the batch.</summary>
    [JsonPropertyName("results")]
    public List<BatchExtractItem> Results { get; set; } = [];
}

/// <summary>
/// Represents a file to be included in a batch extraction request.
/// Provide a readable stream, the MIME type, and an optional file name.
/// </summary>
public sealed class ExtractFile
{
    /// <summary>Readable stream containing the file data (PDF, PNG, JPEG, or TIFF).</summary>
    public required Stream Stream { get; init; }

    /// <summary>MIME type of the file (e.g. "application/pdf", "image/png", "image/jpeg").</summary>
    public required string MimeType { get; init; }

    /// <summary>Optional file name for identification in batch results.</summary>
    public string? FileName { get; init; }
}
