package sk.epostak.sdk;

import com.sun.net.httpserver.HttpServer;
import org.junit.jupiter.api.Test;

import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

final class ErrorFieldsTest {
    private static final String FULL_BODY = "{\"error\":{\"code\":\"DOCUMENT_INVALID\",\"message\":\"unitPrice must be a number\","
            + "\"retryable\":false,\"nextAction\":\"Correct the indicated document field and retry.\","
            + "\"apiCode\":\"VALIDATION_ERROR\",\"message_sk\":\"Jednotková cena musí byť číslo\","
            + "\"fields\":[{\"path\":\"items[0].unitPrice\",\"message\":\"unitPrice must be a number\","
            + "\"message_sk\":\"Jednotková cena musí byť číslo\"}],"
            + "\"rule\":\"PEPPOL-EN16931-R003\",\"fix_hint\":\"Send unitPrice as a number.\","
            + "\"fix_hint_sk\":\"Pošlite unitPrice ako číslo.\",\"docs\":\"https://epostak.sk/api/docs\","
            + "\"requestId\":\"request-ubl-validation\"}}";

    @Test
    void parsesAdditiveErrorFields() throws Exception {
        EPostakException e = fail(422, FULL_BODY);
        assertEquals("DOCUMENT_INVALID", e.getCode());
        assertEquals("unitPrice must be a number", e.getMessage());
        assertEquals("Correct the indicated document field and retry.", e.getNextAction());
        assertEquals(Boolean.FALSE, e.isRetryable());
        assertEquals("request-ubl-validation", e.getRequestId());
        assertEquals("VALIDATION_ERROR", e.getApiCode());
        assertEquals("Jednotková cena musí byť číslo", e.getMessageSk());
        assertEquals(1, e.getFields().size());
        assertEquals("items[0].unitPrice", e.getFields().get(0).get("path"));
        assertEquals("PEPPOL-EN16931-R003", e.getRule());
        assertEquals("Send unitPrice as a number.", e.getFixHint());
        assertEquals("Pošlite unitPrice ako číslo.", e.getFixHintSk());
        assertEquals("https://epostak.sk/api/docs", e.getDocs());
    }

    @Test
    void missingOrWrongTypedFieldsAreNull() throws Exception {
        EPostakException e = fail(422, "{\"error\":{\"code\":\"DOCUMENT_INVALID\",\"message\":\"bad\",\"requestId\":\"r1\"}}");
        assertEquals("DOCUMENT_INVALID", e.getCode());
        assertEquals("r1", e.getRequestId());
        assertNull(e.getApiCode());
        assertNull(e.getMessageSk());
        assertTrue(e.getFields().isEmpty());
        assertNull(e.getRule());
        assertNull(e.getFixHint());
        assertNull(e.getFixHintSk());
        assertNull(e.getDocs());

        EPostakException w = fail(422, "{\"error\":{\"code\":\"X\",\"message\":\"bad\",\"apiCode\":5,\"message_sk\":{},"
                + "\"fields\":\"nope\",\"rule\":true,\"fix_hint\":[],\"docs\":1}}");
        assertNull(w.getApiCode());
        assertNull(w.getMessageSk());
        assertTrue(w.getFields().isEmpty());
        assertNull(w.getRule());
        assertNull(w.getFixHint());
        assertNull(w.getDocs());
    }

    @Test
    void subclassesKeepWorkingAndCarryNewFields() throws Exception {
        EPostakException e = fail(422, "{\"error\":{\"code\":\"UBL_VALIDATION_ERROR\",\"message\":\"bad\","
                + "\"rule\":\"BR_02\",\"apiCode\":\"UBL\",\"message_sk\":\"zle\"}}");
        assertTrue(e instanceof UblValidationException);
        assertEquals("BR_02", ((UblValidationException) e).getRule());
        assertEquals("BR_02", e.getRule());
        assertEquals("UBL", e.getApiCode());
        assertEquals("zle", e.getMessageSk());

        EPostakException d = fail(409, "{\"error\":{\"code\":\"DUPLICATE_INVOICE_NUMBER\",\"message\":\"dup\","
                + "\"fix_hint\":\"Use another number.\"}}");
        assertTrue(d instanceof DuplicateInvoiceNumberException);
        assertEquals("Use another number.", d.getFixHint());
        assertFalse(d.getFields() == null);
    }

    private static EPostakException fail(int status, String body) throws Exception {
        HttpServer server = HttpServer.create(new InetSocketAddress(InetAddress.getLoopbackAddress(), 0), 0);
        server.createContext("/", exchange -> {
            boolean auth = exchange.getRequestURI().getPath().equals("/sapi/v1/auth/token");
            byte[] bytes = (auth
                    ? "{\"access_token\":\"token\",\"refresh_token\":\"refresh\",\"expires_in\":3600}"
                    : body).getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            exchange.sendResponseHeaders(auth ? 200 : status, bytes.length);
            exchange.getResponseBody().write(bytes);
            exchange.close();
        });
        server.start();
        try {
            EPostak client = EPostak.builder()
                    .clientId("sk_int_test")
                    .clientSecret("sk_int_test")
                    .baseUrl("http://127.0.0.1:" + server.getAddress().getPort() + "/api/v1")
                    .firmId("firm-123")
                    .build();
            return assertThrows(EPostakException.class, () -> client.documents().get("doc-1"));
        } finally {
            server.stop(0);
        }
    }
}
