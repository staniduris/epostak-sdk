package sk.epostak.sdk.models;

/**
 * Response from sending an invoice response (accept/reject/query/etc.).
 *
 * @param documentId      the document UUID that was responded to
 * @param responseStatus  the response status that was sent: one of {@code AB},
 *                        {@code IP}, {@code UQ}, {@code CA}, {@code RE},
 *                        {@code AP}, {@code PD}
 * @param respondedAt     ISO 8601 timestamp of when the response was sent
 * @param peppolMessageId AS4 message ID assigned to the response dispatch, or
 *                        {@code null} if dispatch failed and the response was queued
 * @param dispatchStatus  {@code "sent"} when the AS4 envelope was successfully
 *                        transmitted, or {@code "failed_queued"} if transport
 *                        failed and the response was persisted for retry (HTTP 202)
 * @param dispatchError   error message describing the transport failure, present
 *                        only when {@code dispatchStatus == "failed_queued"};
 *                        otherwise {@code null}
 */
public record InvoiceRespondResponse(
        String documentId,
        String responseStatus,
        String respondedAt,
        String peppolMessageId,
        String dispatchStatus,
        String dispatchError
) {}
