package sk.epostak.sdk.models;

import java.util.List;

/**
 * Response from batch assigning firms to an integrator. Returned with HTTP 200.
 *
 * @param results per-ICO assignment results (one entry per ICO in the request)
 */
public record BatchAssignResponse(
        List<BatchAssignResult> results
) {
    /**
     * Result for a single ICO in a batch assign request.
     * <p>
     * On success, {@code firm} and {@code status} are populated and {@code error}
     * / {@code message} are {@code null}. On failure (e.g. ICO not found, firm
     * requires OAuth consent), {@code firm} and {@code status} are {@code null}
     * and {@code error} holds the machine-readable code.
     *
     * @param ico     the ICO that was processed
     * @param firm    the assigned firm details, or {@code null} on error
     * @param status  assignment status: {@code "active"} on first claim /
     *                reactivation, {@code "already_assigned"} when the link
     *                was already active; {@code null} on error
     * @param error   error code if assignment failed (e.g. {@code "NOT_FOUND"},
     *                {@code "FORBIDDEN"}), or {@code null}
     * @param message human-readable error message, or {@code null}
     */
    public record BatchAssignResult(
            String ico,
            AssignFirmResponse.AssignedFirm firm,
            String status,
            String error,
            String message
    ) {}
}
