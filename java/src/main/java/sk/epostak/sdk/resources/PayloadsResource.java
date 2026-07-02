package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.BatchExtractResult;
import sk.epostak.sdk.models.ConvertResult;
import sk.epostak.sdk.models.ExtractResult;
import sk.epostak.sdk.models.ParsedInvoice;
import sk.epostak.sdk.models.ValidationResult;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Payload Assistant helpers for OCR, parse, convert, and validation. */
public final class PayloadsResource {

    private final HttpClient http;

    public PayloadsResource(HttpClient http) {
        this.http = http;
    }

    public ExtractResult extract(byte[] fileBytes, String fileName, String mimeType) {
        return http.postMultipart("/payloads/extract", fileBytes, fileName, mimeType, ExtractResult.class);
    }

    public BatchExtractResult extractBatch(List<ExtractResource.FileInput> files) {
        List<HttpClient.FileUpload> uploads = files.stream()
                .map(f -> new HttpClient.FileUpload(f.data(), f.fileName(), f.mimeType()))
                .toList();
        return http.postMultipartBatch("/payloads/extract/batch", uploads, BatchExtractResult.class);
    }

    public ParsedInvoice parse(String xml) {
        return http.post("/payloads/parse", Map.of("xml", xml), ParsedInvoice.class);
    }

    public ConvertResult convert(String inputFormat, String outputFormat, Object document) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("input_format", inputFormat);
        body.put("output_format", outputFormat);
        body.put("document", document);
        return http.post("/payloads/convert", body, ConvertResult.class);
    }

    public ValidationResult validate(Object request) {
        return http.post("/payloads/validate", request, ValidationResult.class);
    }
}
