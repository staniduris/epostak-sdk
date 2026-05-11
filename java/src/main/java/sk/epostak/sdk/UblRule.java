package sk.epostak.sdk;

/**
 * Well-known UBL / Peppol BIS 3.0 schematron rule identifiers that the API
 * returns in a 422 {@code UBL_VALIDATION_ERROR} response.
 *
 * <pre>{@code
 * try {
 *     client.documents().send(req);
 * } catch (UblValidationException e) {
 *     if (UblRule.BR_02.name().equals(e.getRule())) {
 *         System.err.println("Invoice must have a profile identifier");
 *     }
 * }
 * }</pre>
 */
public enum UblRule {

    /**
     * BR-02 — An Invoice shall have a Profile identifier.
     */
    BR_02,

    /**
     * BR-05 — An Invoice shall have an Invoice type code.
     */
    BR_05,

    /**
     * BR-06 — An Invoice shall have a VAT accounting currency code.
     */
    BR_06,

    /**
     * BR-11 — Every Invoice line (BG-25) shall have an Invoice line identifier.
     */
    BR_11,

    /**
     * BR-16 — Each Invoice line (BG-25) shall have an Invoice line net amount.
     */
    BR_16,

    /**
     * BT-1 — The Invoice number (BT-1) shall be a unique identification of the Invoice
     * in the context of the Seller.
     */
    BT_1,

    /**
     * PEPPOL-R008 — The seller's registration identifier scheme identifier must be within
     * the Peppol code list.
     */
    PEPPOL_R008
}
