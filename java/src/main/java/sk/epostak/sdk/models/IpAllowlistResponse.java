package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * Response from {@code GET} / {@code PUT /auth/ip-allowlist}.
 * <p>
 * An empty list means no IP restriction is in effect — any caller IP is
 * accepted. When the list is non-empty, requests authenticated with this key
 * are rejected (HTTP 403) unless the source IP matches at least one entry.
 * Each entry is either a bare IPv4/IPv6 address or a CIDR block
 * ({@code addr/prefix}). Maximum 50 entries.
 *
 * @param ipAllowlist bare IP addresses or CIDR blocks; empty list means no restriction
 */
public record IpAllowlistResponse(
        @SerializedName("ip_allowlist") List<String> ipAllowlist
) {}
