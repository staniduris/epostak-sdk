package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Response from batch assigning firms to an integrator.
 *
 * @param results per-ICO assignment results (one entry per ICO in the request)
 */
public record BatchAssignResponse(
        List<BatchAssignResult> results
) {
    /**
     * Result for a single ICO in a batch assign request.
     *
     * @param ico     the ICO that was processed
     * @param firm    the assigned firm details, or {@code null} on error
     * @param status  assignment status, e.g. {@code "assigned"}, {@code "already_assigned"}, {@code "error"}
     * @param error   error code if assignment failed, or {@code null}
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
