package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * Generic cursor-paginated page. Used by endpoints that walk
 * {@code (timestamp DESC, id DESC)} keysets — pass {@link #nextCursor()} from
 * one page back into the next call until {@link #hasMore()} is {@code false}
 * (or {@link #nextCursor()} comes back as {@code null}).
 *
 * @param <T>        item type
 * @param items      items in the current page, newest first
 * @param nextCursor opaque cursor for the next page, or {@code null} when there is no next page
 * @param hasMore    whether more pages are available
 */
public record CursorPage<T>(
        List<T> items,
        @SerializedName("next_cursor") String nextCursor,
        @SerializedName("has_more") boolean hasMore
) {}
