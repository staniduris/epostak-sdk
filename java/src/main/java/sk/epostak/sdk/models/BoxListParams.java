package sk.epostak.sdk.models;

public record BoxListParams(
        String status,
        String direction,
        Integer limit,
        Integer offset
) {}
