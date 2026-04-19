package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.*;

import java.util.LinkedHashMap;
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
     * Send an invoice response (accept, reject, or query) for a received document.
     *
     * <pre>{@code
     * InvoiceRespondResponse resp = client.documents().respond("doc_abc123",
     *     new InvoiceRespondRequest("AP", "Accepted, thank you"));
     * }</pre>
     *
     * @param id      the document UUID of the received invoice
     * @param request the response containing status code and optional note
     * @return the respond confirmation with timestamp
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public InvoiceRespondResponse respond(String id, InvoiceRespondRequest request) {
        return http.post("/documents/" + HttpClient.encode(id) + "/respond", request, InvoiceRespondResponse.class);
    }

    /**
     * Validate a document without sending it. Returns validation warnings
     * and, for JSON mode requests, the generated UBL XML.
     *
     * @param request the document to validate (same shape as send)
     * @return the validation result with warnings and optional UBL
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public ValidationResult validate(SendDocumentRequest request) {
        return http.post("/documents/validate", request, ValidationResult.class);
    }

    /**
     * Check whether a receiver supports Peppol document delivery (preflight check).
     *
     * <pre>{@code
     * PreflightResult result = client.documents().preflight("0245:1234567890");
     * if (result.registered()) {
     *     // Safe to send
     * }
     * }</pre>
     *
     * @param receiverPeppolId receiver Peppol ID, e.g. {@code "0245:1234567890"}
     * @param documentTypeId   optional UBL document type identifier to check specific capability
     * @return the preflight result indicating registration and capability support
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public PreflightResult preflight(String receiverPeppolId, String documentTypeId) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("receiver_peppol_id", receiverPeppolId);
        if (documentTypeId != null) {
            body.put("document_type_id", documentTypeId);
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
}
