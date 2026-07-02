using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Extract
// ---------------------------------------------------------------------------

/// <summary>Field the integrator must review or complete before sending.</summary>
public sealed class ExtractMissingField
{
    /// <summary>Machine-readable field key, for example <c>receiverPeppolId</c>.</summary>
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    /// <summary>Human-readable label for review UIs.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>Explanation of why the value is needed.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>True when the field blocks <c>/documents/send</c>.</summary>
    [JsonPropertyName("blocking")]
    public bool? Blocking { get; set; }

    /// <summary>Current value, when a partial value exists.</summary>
    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

/// <summary>Provenance for a value returned by OCR or enrichment.</summary>
public sealed class ExtractFieldSource
{
    /// <summary>Source identifier, for example <c>ocr</c>, <c>firm_profile</c>, or <c>peppol_directory</c>.</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    /// <summary>Resolved value from this source.</summary>
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    /// <summary>Optional source confidence score (0.0 - 1.0).</summary>
    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }
}

/// <summary>Recommended next action after OCR extraction.</summary>
public sealed class ExtractNextAction
{
    /// <summary>Machine-readable action type.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>Human-readable action label.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>Longer review/send instruction.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>API endpoint to call next, when applicable.</summary>
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    /// <summary>HTTP method for <see cref="Endpoint"/>, when applicable.</summary>
    [JsonPropertyName("method")]
    public string? Method { get; set; }
}

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

    /// <summary>Resolved document type, for example <c>invoice</c>, <c>credit_note</c>, or <c>self_billing</c>.</summary>
    [JsonPropertyName("document_type")]
    public string? DocumentType { get; set; }

    /// <summary><c>inbound</c> returns generated UBL; <c>outbound</c> returns a reviewable send payload when supported.</summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    /// <summary>Draft JSON body for <c>POST /documents/send</c>; returned for outbound standard-invoice OCR.</summary>
    [JsonPropertyName("send_payload")]
    public Dictionary<string, object>? SendPayload { get; set; }

    /// <summary>Fields the caller must fill before posting <see cref="SendPayload"/>.</summary>
    [JsonPropertyName("send_payload_missing_fields")]
    public List<string> SendPayloadMissingFields { get; set; } = [];

    /// <summary>True when <see cref="SendPayload"/> has the blocking fields needed by <c>/documents/send</c>.</summary>
    [JsonPropertyName("send_ready")]
    public bool? SendReady { get; set; }

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

    /// <summary>Review checklist for missing or low-confidence values.</summary>
    [JsonPropertyName("missing_fields")]
    public List<ExtractMissingField> MissingFields { get; set; } = [];

    /// <summary>Provenance map for extracted/enriched fields.</summary>
    [JsonPropertyName("field_sources")]
    public Dictionary<string, ExtractFieldSource> FieldSources { get; set; } = [];

    /// <summary>Recommended next API action.</summary>
    [JsonPropertyName("next_action")]
    public ExtractNextAction? NextAction { get; set; }

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

    /// <summary>Resolved document type. Null if unavailable or extraction failed.</summary>
    [JsonPropertyName("document_type")]
    public string? DocumentType { get; set; }

    /// <summary>Document direction: <c>inbound</c> or <c>outbound</c>.</summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    /// <summary>Draft JSON body for <c>POST /documents/send</c>. Null when unavailable or not supported.</summary>
    [JsonPropertyName("send_payload")]
    public Dictionary<string, object>? SendPayload { get; set; }

    /// <summary>Fields the caller must fill before posting <see cref="SendPayload"/>.</summary>
    [JsonPropertyName("send_payload_missing_fields")]
    public List<string> SendPayloadMissingFields { get; set; } = [];

    /// <summary>True when <see cref="SendPayload"/> has the blocking fields needed by <c>/documents/send</c>.</summary>
    [JsonPropertyName("send_ready")]
    public bool? SendReady { get; set; }

    /// <summary>Structured extraction data. Null if extraction failed.</summary>
    [JsonPropertyName("extraction")]
    public Dictionary<string, object>? Extraction { get; set; }

    /// <summary>Generated UBL XML. Null if extraction failed.</summary>
    [JsonPropertyName("ubl_xml")]
    public string? UblXml { get; set; }

    /// <summary>AI confidence score as a string. Null if extraction failed.</summary>
    [JsonPropertyName("confidence")]
    public string? Confidence { get; set; }

    /// <summary>Per-field numeric confidence scores. Null if extraction failed.</summary>
    [JsonPropertyName("confidence_scores")]
    public Dictionary<string, double>? ConfidenceScores { get; set; }

    /// <summary>True when the extraction should be manually reviewed.</summary>
    [JsonPropertyName("needs_review")]
    public bool? NeedsReview { get; set; }

    /// <summary>Review checklist for missing or low-confidence values.</summary>
    [JsonPropertyName("missing_fields")]
    public List<ExtractMissingField> MissingFields { get; set; } = [];

    /// <summary>Provenance map for extracted/enriched fields.</summary>
    [JsonPropertyName("field_sources")]
    public Dictionary<string, ExtractFieldSource> FieldSources { get; set; } = [];

    /// <summary>Recommended next API action.</summary>
    [JsonPropertyName("next_action")]
    public ExtractNextAction? NextAction { get; set; }

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
