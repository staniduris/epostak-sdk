package sk.epostak.sdk.models;

import java.util.List;

/**
 * Result of a preflight check on a Peppol receiver. Reports whether the
 * receiver is reachable on the network, whether the requested document type
 * is in their advertised set, and whether the supplied document (when any)
 * passes Peppol BIS validation.
 *
 * @param canSend                       convenience aggregate: {@code true} when sending
 *                                      is likely to succeed (recipient found, accepts the
 *                                      document type, and validation did not fail)
 * @param recipientFound                {@code true} when the SMP lookup returned a
 *                                      registered endpoint for the receiver
 * @param recipientAcceptsDocumentType  {@code true} when the receiver advertises the
 *                                      probed document type; {@code false} when they
 *                                      don't; {@code null} when no specific document
 *                                      type was probed
 * @param validationPassed              {@code true} when the supplied document passed
 *                                      Peppol BIS validation; {@code false} on hard
 *                                      errors; {@code null} when the validator was
 *                                      skipped or unavailable
 * @param validationErrors              list of Peppol BIS validation error messages
 *                                      (empty when the document passed)
 * @param warnings                      non-fatal warnings, e.g. validator offline
 */
public record PreflightResult(
        boolean canSend,
        Boolean recipientFound,
        Boolean recipientAcceptsDocumentType,
        Boolean validationPassed,
        List<String> validationErrors,
        List<String> warnings
) {}
