<?php

declare(strict_types=1);

namespace EPostak;

/**
 * UBL validation rule codes returned in {@see UblValidationException::$rule}.
 *
 * These map to the `error.rule` field in the API's 422 `UBL_VALIDATION_ERROR`
 * response body. Source of truth: `lib/ubl/generate.ts` in the epostak repo.
 *
 * New rules may be added in future API versions — treat these constants as a
 * hint, not a closed enum.
 */
final class UblRule
{
    /** BT-2 — Invoice issue date is mandatory. */
    public const BR_02 = 'BR-02';

    /** BT-27 — Seller name is mandatory. */
    public const BR_05 = 'BR-05';

    /** BT-44 — Buyer name is mandatory. Pass `receiverName` in the request body. */
    public const BR_06 = 'BR-06';

    /** BT-31 / BT-32 — Seller VAT identifier required for VAT-rated invoices. */
    public const BR_11 = 'BR-11';

    /** Invoice must have at least one line. */
    public const BR_16 = 'BR-16';

    /** BT-1 — Invoice number must not be empty. */
    public const BT_1 = 'BT-1';

    /** EndpointID is empty — firm must have DIČ, IČO, or a registered Peppol ID. */
    public const PEPPOL_R008 = 'PEPPOL-R008';
}
