package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.*;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Send and receive documents via Peppol.
 * <p>
 * Provides operations for sending invoices, checking delivery status,
 * downloading PDF/UBL representations, and responding to received invoices.
 * <p>
 * Access via {@code client.documents()}.
 *
 * <pre>{@code
 * // Send an invoice
 * SendDocumentResponse result = client.documents().send(
 *     SendDocumentRequest.builder("0245:1234567890")
 *         .invoiceNumber("FV-2026-001")
 *         .issueDate("2026-04-04")
 *         .items(List.of(new SendDocumentRequest.LineItem(
 *             "Consulting", 10, 50, 23)))
 *         .build());
 *
 * // Check status
 * DocumentStatusResponse status = client.documents().status(result.documentId());
 * }</pre>
 */
public final class DocumentsResource {

    private final HttpClient http;
    private final InboxResource inbox;

    /**
     * Creates a new documents resource.
     *
     * @param http the HTTP client used for API communication
     */
    public DocumentsResource(HttpClient http) {
        this.http = http;
        this.inbox = new InboxResource(http);
    }

    /**
     * Access received (inbound) documents.
     *
     * @return the inbox sub-resource
     */
    public InboxResource inbox() {
        return inbox;
    }

    /**
     * Get a document by ID.
     *
     * <pre>{@code
     * Document doc = client.documents().get("doc_abc123");
     * System.out.println(doc.getStatus()); // "DELIVERED"
     * }</pre>
     *
     * @param id the document UUID
     * @return the full document object
     * @throws sk.epostak.sdk.EPostakException if the document is not found (404) or the request fails
     */
    public Document get(String id) {
        return http.get("/documents/" + HttpClient.encode(id), Document.class);
    }

    /**
     * Update a draft document. Only documents in {@code DRAFT} status can be updated.
     *
     * @param id      the document UUID
     * @param request the fields to update (all optional)
     * @return the updated document
     * @throws sk.epostak.sdk.EPostakException if the document is not a draft or the request fails
     */
    public Document update(String id, UpdateDocumentRequest request) {
        return http.patch("/documents/" + HttpClient.encode(id), request, Document.class);
    }

    /**
     * Send a document via Peppol. Supports both JSON mode (structured data) and
     * XML mode (pre-built UBL).
     *
     * <pre>{@code
     * SendDocumentResponse result = client.documents().send(
     *     SendDocumentRequest.builder("0245:1234567890")
     *         .invoiceNumber("FV-2026-001")
     *         .issueDate("2026-04-04")
     *         .dueDate("2026-04-18")
     *         .items(List.of(new SendDocumentRequest.LineItem(
     *             "Consulting services", 10, 150.0, 23)))
     *         .build());
     * }</pre>
     *
     * @param request the send document request
     * @return the response containing document ID, message ID, and initial status
     * @throws sk.epostak.sdk.EPostakException if validation fails or the request fails
     */
    public SendDocumentResponse send(SendDocumentRequest request) {
        return http.post("/documents/send", request, SendDocumentResponse.class);
    }

    /**
     * Get full document status including status history timeline.
     *
     * @param id the document UUID
     * @return the status response with history entries
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public DocumentStatusResponse status(String id) {
        return http.get("/documents/" + HttpClient.encode(id) + "/status", DocumentStatusResponse.class);
    }

    /**
     * Get delivery evidence for a sent document, including AS4 receipt and
     * Message Level Response (MLR) data.
     *
     * @param id the document UUID
     * @return the evidence response with receipts and response documents
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public DocumentEvidenceResponse evidence(String id) {
        return http.get("/documents/" + HttpClient.encode(id) + "/evidence", DocumentEvidenceResponse.class);
    }

    /**
     * Download the document rendered as PDF bytes.
     *
     * <pre>{@code
     * byte[] pdfBytes = client.documents().pdf("doc_abc123");
     * Files.write(Path.of("invoice.pdf"), pdfBytes);
     * }</pre>
     *
     * @param id the document UUID
     * @return raw PDF bytes
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public byte[] pdf(String id) {
        return http.getBytes("/documents/" + HttpClient.encode(id) + "/pdf");
    }

    /**
     * Download the UBL XML representation of the document.
     *
     * @param id the document UUID
     * @return the UBL XML as a string
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public String ubl(String id) {
        return http.getString("/documents/" + HttpClient.encode(id) + "/ubl");
    }

    /**
     * Download the signed AS4 envelope for this document from the 10-year WORM
     * archive (S3 Object Lock COMPLIANCE). Returns the raw multipart AS4 payload
     * exactly as it was transmitted on the Peppol network — signed, timestamped,
     * and tamper-evident. Usable as dispute evidence or for regulatory retention.
     * <p>
     * Availability: {@code api-enterprise} plan only; other plans get
     * {@link sk.epostak.sdk.EPostakException} with HTTP 403.
     * <p>
     * Very recently sent documents may briefly return 404 until the archival
     * cron picks them up — retry after a short delay rather than treating the
     * first 404 as permanent.
     *
     * <pre>{@code
     * byte[] as4 = client.documents().envelope("doc_abc123");
     * Files.write(Path.of("doc_abc123.as4"), as4);
     * }</pre>
     *
     * @param id the document UUID
     * @return raw AS4 envelope bytes (multipart payload)
     * @throws sk.epostak.sdk.EPostakException if the document is not found, the envelope
     *         is not yet archived, the plan is not {@code api-enterprise}, or the request fails
     */
    public byte[] envelope(String id) {
        return http.getBytes("/documents/" + HttpClient.encode(id) + "/envelope");
    }

    /**
     * Send an invoice response for a received document. Status must be one of
     * the seven UBL response codes: {@code AB}, {@code IP}, {@code UQ},
     * {@code CA}, {@code RE}, {@code AP}, {@code PD}. See
     * {@link InvoiceRespondRequest} for the full legend.
     *
     * <pre>{@code
     * InvoiceRespondResponse resp = client.documents().respond("doc_abc123",
     *     new InvoiceRespondRequest("AP", "Accepted, thank you"));
     * }</pre>
     *
     * @param id      the document UUID of the received invoice
     * @param request the response containing status code and optional note (max 500 chars)
     * @return the respond confirmation with timestamp and dispatch status
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public InvoiceRespondResponse respond(String id, InvoiceRespondRequest request) {
        return http.post("/documents/" + HttpClient.encode(id) + "/respond", request, InvoiceRespondResponse.class);
    }

    /**
     * Validate a UBL XML document against Peppol BIS 3.0 rules without sending it.
     *
     * <pre>{@code
     * ValidationResult result = client.documents().validate(ublXml);
     * if (!result.valid()) {
     *     result.errors().forEach(e -> System.err.println(e.rule() + ": " + e.message()));
     * }
     * }</pre>
     *
     * @param ublXml the UBL 2.1 XML document to validate
     * @return the validation result with per-rule errors and warnings
     * @throws sk.epostak.sdk.EPostakException if the request fails or the validator is unavailable
     */
    public ValidationResult validate(String ublXml) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("format", "ubl");
        body.put("document", ublXml);
        return http.post("/documents/validate", body, ValidationResult.class);
    }

    /**
     * Validate a JSON-mode invoice body by generating UBL server-side and running
     * full Peppol BIS 3.0 validation against it.
     *
     * @param jsonDocument the invoice document (same shape as {@link SendDocumentRequest}, as a map/POJO)
     * @return the validation result with per-rule errors and warnings
     * @throws sk.epostak.sdk.EPostakException if the request fails or the validator is unavailable
     */
    public ValidationResult validateJson(Object jsonDocument) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("format", "json");
        body.put("document", jsonDocument);
        return http.post("/documents/validate", body, ValidationResult.class);
    }

    /**
     * Check whether a receiver supports Peppol document delivery (preflight check).
     *
     * <pre>{@code
     * PreflightResult result = client.documents().preflight("0245:1234567890");
     * if (result.canSend()) {
     *     // Safe to send
     * }
     * }</pre>
     *
     * @param receiverPeppolId receiver Peppol ID, e.g. {@code "0245:1234567890"}
     * @param documentType     optional UBL document type identifier to check specific capability
     * @return the preflight result indicating registration and capability support
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public PreflightResult preflight(String receiverPeppolId, String documentType) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("receiverPeppolId", receiverPeppolId);
        if (documentType != null) {
            body.put("documentType", documentType);
        }
        return http.post("/documents/preflight", body, PreflightResult.class);
    }

    /**
     * Check whether a receiver supports Peppol document delivery, for any document type.
     *
     * @param receiverPeppolId receiver Peppol ID, e.g. {@code "0245:1234567890"}
     * @return the preflight result indicating registration status
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public PreflightResult preflight(String receiverPeppolId) {
        return preflight(receiverPeppolId, null);
    }

    /**
     * Convert between JSON and UBL XML document representations.
     *
     * <pre>{@code
     * // JSON to UBL
     * ConvertResult ubl = client.documents().convert("json", "ubl", jsonData);
     *
     * // UBL to JSON
     * ConvertResult json = client.documents().convert("ubl", "json", xmlString);
     * }</pre>
     *
     * @param inputFormat  the format of {@code document}: {@code "json"} or {@code "ubl"}
     * @param outputFormat the desired output format: {@code "ubl"} or {@code "json"}
     * @param document     the document to convert — a {@code Map}/POJO for JSON input,
     *                     or a UBL XML {@code String} for UBL input
     * @return the conversion result containing {@code outputFormat}, {@code document}, and {@code warnings}
     * @throws sk.epostak.sdk.EPostakException if the conversion fails or the request fails
     */
    public ConvertResult convert(String inputFormat, String outputFormat, Object document) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("input_format", inputFormat);
        body.put("output_format", outputFormat);
        body.put("document", document);
        return http.post("/documents/convert", body, ConvertResult.class);
    }

    /**
     * Send multiple documents in a single call. Items are processed independently:
     * a failure on one item does not abort the batch. The response contains a
     * per-item status in the same order as the request.
     *
     * <pre>{@code
     * BatchSendResponse resp = client.documents().sendBatch(List.of(
     *     new BatchSendRequest.BatchItem(
     *         SendDocumentRequest.builder("0245:12345678")
     *             .invoiceNumber("FV-2026-001").build()),
     *     new BatchSendRequest.BatchItem(
     *         SendDocumentRequest.builder("0245:87654321")
     *             .invoiceNumber("FV-2026-002").build(), "idem-002")
     * ));
     * System.out.println(resp.succeeded() + "/" + resp.total() + " sent");
     * }</pre>
     *
     * @param items the batch items to send; max 50 per call, 20 MB total body size
     * @return the batch send response with per-item results
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public BatchSendResponse sendBatch(List<BatchSendRequest.BatchItem> items) {
        return http.post("/documents/send/batch", new BatchSendRequest(items), BatchSendResponse.class);
    }

    /**
     * Parse a UBL XML invoice into structured JSON without persisting or sending it.
     * The returned shape matches the JSON side of {@link #convert(String, String, Object)}
     * with {@code output_format=json}.
     *
     * <pre>{@code
     * ParsedInvoice parsed = client.documents().parse(ublXmlString);
     * System.out.println(parsed.document().get("invoice_number"));
     * }</pre>
     *
     * @param xml the UBL 2.1 XML document
     * @return the parsed invoice and any non-fatal warnings
     * @throws sk.epostak.sdk.EPostakException if the XML cannot be parsed or the request fails
     */
    public ParsedInvoice parse(String xml) {
        return http.postRaw("/documents/parse", xml, "application/xml", ParsedInvoice.class);
    }

    /**
     * Mark the processing state of an inbound document.
     * <p>
     * {@code state} must be one of {@code "delivered"}, {@code "processed"},
     * {@code "failed"}, or {@code "read"}. The optional {@code note} is recorded
     * alongside the transition (e.g. failure reason).
     *
     * <pre>{@code
     * MarkResponse r = client.documents().mark("doc_abc123", "processed", "Booked to ledger");
     * System.out.println(r.status()); // overall doc status after marking
     * }</pre>
     *
     * @param id    the document UUID to mark
     * @param state target state: {@code "delivered"}, {@code "processed"}, {@code "failed"}, or {@code "read"}
     * @param note  optional note (max 500 chars), or {@code null}
     * @return the mark response with updated timestamps
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the state transition is invalid
     */
    public MarkResponse mark(String id, String state, String note) {
        return http.post(
                "/documents/" + HttpClient.encode(id) + "/mark",
                new MarkRequest(state, note),
                MarkResponse.class
        );
    }

    /**
     * Mark the processing state of an inbound document, without a note.
     *
     * @param id    the document UUID to mark
     * @param state target state: {@code "delivered"}, {@code "processed"}, {@code "failed"}, or {@code "read"}
     * @return the mark response with updated timestamps
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the state transition is invalid
     */
    public MarkResponse mark(String id, String state) {
        return mark(id, state, null);
    }
}
