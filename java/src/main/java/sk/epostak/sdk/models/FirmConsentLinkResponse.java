package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/** Fresh one-time firm-consent URL and immutable offer metadata. */
public record FirmConsentLinkResponse(
        String id,
        @SerializedName("consent_url") String consentUrl,
        @SerializedName("customer_reference") String customerReference,
        @SerializedName("integration_path") String integrationPath,
        @SerializedName("requested_interfaces") List<String> requestedInterfaces,
        List<String> scopes,
        String status,
        @SerializedName("expires_at") String expiresAt,
        @SerializedName("created_at") String createdAt
) {}
