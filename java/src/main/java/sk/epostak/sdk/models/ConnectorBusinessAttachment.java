package sk.epostak.sdk.models;

/** BG-24 attachment embedded into the generated business document. */
public record ConnectorBusinessAttachment(
        String fileName,
        String mimeType,
        String content,
        String description
) {}
