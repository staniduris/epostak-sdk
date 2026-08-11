package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Request for a fresh one-time Enterprise firm-consent URL.
 * Provide exactly one of {@code dic} or {@code ico}. Scopes must include
 * {@code firms:manage} and at least one {@code documents:*} permission.
 */
public record CreateFirmConsentLinkRequest(
        String dic,
        String ico,
        @SerializedName("customer_reference") String customerReference,
        List<String> scopes
) {
    public static CreateFirmConsentLinkRequest forDic(
            String dic,
            String customerReference,
            List<String> scopes
    ) {
        return new CreateFirmConsentLinkRequest(dic, null, customerReference, scopes);
    }

    public static CreateFirmConsentLinkRequest forIco(
            String ico,
            String customerReference,
            List<String> scopes
    ) {
        return new CreateFirmConsentLinkRequest(null, ico, customerReference, scopes);
    }
}
