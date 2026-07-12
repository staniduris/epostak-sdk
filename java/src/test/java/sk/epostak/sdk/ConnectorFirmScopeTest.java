package sk.epostak.sdk;

import com.sun.net.httpserver.Headers;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;
import org.junit.jupiter.api.Test;
import sk.epostak.sdk.models.BoxCreateRequest;
import sk.epostak.sdk.models.BoxListParams;
import sk.epostak.sdk.models.BoxScheduleRequest;
import sk.epostak.sdk.models.ConnectorActionRequest;
import sk.epostak.sdk.models.ConnectorAutopilotRequest;
import sk.epostak.sdk.models.ConnectorListParams;
import sk.epostak.sdk.models.ConnectorMailboxRepairRequest;
import sk.epostak.sdk.models.ConnectorPreflightRequest;
import sk.epostak.sdk.models.ConnectorReconcileParams;
import sk.epostak.sdk.models.ConnectorSendPolicyOptions;
import sk.epostak.sdk.models.ConnectorSubmitDocumentRequest;
import sk.epostak.sdk.models.ConnectorSyncParams;
import sk.epostak.sdk.models.CapabilitiesRequest;
import sk.epostak.sdk.models.SendDocumentRequest;

import java.io.IOException;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;

final class ConnectorFirmScopeTest {
    @Test
    void connectorV2EndpointsDoNotSendGlobalFirmId() throws Exception {
        for (NamedCall call : v2Calls()) {
            try (CaptureServer server = CaptureServer.start()) {
                EPostak client = createClient(server);

                call.invoke(client);

                CapturedRequest request = singleApiRequest(server);
                assertNull(request.firmId(), call.name());
            }
        }
    }

    @Test
    void legacyConnectorEndpointsStillSendGlobalFirmId() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.connector().preflight(new ConnectorPreflightRequest(
                    "0245:1234567890",
                    Map.of()
            ));

            CapturedRequest request = singleApiRequest(server);
            assertEquals("firm-123", request.firmId());
        }
    }

    @Test
    void majorReleaseEnterpriseNamespaceExposesFullPlatformResources() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            assertEquals(client.documents(), client.enterprise().documents());
            assertEquals(client.documents().inbox(), client.enterprise().inbox());
            assertEquals(client.inbound(), client.enterprise().pull().inbound());
            assertEquals(client.outbound(), client.enterprise().pull().outbound());
            assertEquals(client.connector(), client.enterprise().connector());
            assertEquals(client.webhooks(), client.enterprise().webhooks());
        }
    }

    @Test
    void customerScopedConnectorSubmitDocumentOmitsFirmIdAndDefaultsToStage() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.enterprise().connector().customers().forCustomer("erp-customer-1").submitDocument(
                    new ConnectorSubmitDocumentRequest(
                            "FA-1",
                            "erp-fa-1",
                            Map.of("invoiceNumber", "FA-1")
                    )
            );

            CapturedRequest request = singleApiRequest(server);
            assertEquals("/api/v1/connector/autopilot", request.path());
            assertEquals(null, request.firmId());
            assertEquals(true, request.body().contains("\"customerRef\":\"erp-customer-1\""));
            assertEquals(true, request.body().contains("\"mode\":\"stage\""));
        }
    }

    @Test
    void sapiParticipantDocumentsSendUsesSapiBaseAndParticipantHeader() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.sapi().participants().forParticipant("0245:1234567890")
                    .documents()
                    .send(Map.of("xml", "<Invoice/>"), "sapi-fa-1");

            CapturedRequest request = singleNonAuthRequest(server);
            assertEquals("/sapi/v1/document/send", request.path());
            assertEquals("0245:1234567890", request.participantId());
            assertEquals("sapi-fa-1", request.idempotencyKey());
        }
    }

    @Test
    void jsonModeDocumentsSendRequiresReceiverNameBeforeHttp() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            IllegalArgumentException error = assertThrows(IllegalArgumentException.class, () ->
                    client.documents().send(
                            SendDocumentRequest.builder("0245:12345678")
                                    .items(List.of(new SendDocumentRequest.LineItem("Item", 1, 100, 23)))
                                    .build()
                    )
            );

            assertEquals(true, error.getMessage().contains("receiverName"));
            assertEquals(0, server.requests().size());
        }
    }

    @Test
    void xmlModeDocumentsSendDoesNotRequireReceiverName() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.documents().send(
                    SendDocumentRequest.builder("0245:12345678")
                            .xml("<Invoice/>")
                            .build()
            );

            CapturedRequest request = singleNonAuthRequest(server);
            assertEquals("/api/v1/documents/send", request.path());
            assertEquals(false, request.body().contains("receiverName"));
        }
    }

    @Test
    void peppolCapabilitiesUsesParticipantEnvelope() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.peppol().capabilities(new CapabilitiesRequest(
                    "0245",
                    "2020305606",
                    "urn:invoice"
            ));

            CapturedRequest request = singleNonAuthRequest(server);
            assertEquals("/api/v1/peppol/capabilities", request.path());
            assertEquals(true, request.body().contains("\"participant\""));
            assertEquals(true, request.body().contains("\"scheme\":\"0245\""));
            assertEquals(true, request.body().contains("\"identifier\":\"2020305606\""));
            assertEquals(true, request.body().contains("\"documentType\":\"urn:invoice\""));
        }
    }

    @Test
    void boxResourceUsesPublicBoxPaths() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.box().list(new BoxListParams("ready", "outbound", 10, 5));
            client.box().create(new BoxCreateRequest(
                    "<Invoice/>",
                    "2026-07-01T00:00:00.000Z",
                    "erp-doc-1",
                    Map.of("source", "sdk-test")
            ));
            client.box().get("box-1");
            client.box().schedule("box-1", new BoxScheduleRequest("2026-07-01T00:00:00.000Z"));
            client.box().sendNow("box-1");
            client.box().retry("box-1");
            client.box().cancel("box-1");

            List<CapturedRequest> apiRequests = server.requests().stream()
                    .filter(request -> !request.path().equals("/sapi/v1/auth/token"))
                    .toList();
            assertEquals(7, apiRequests.size());
            assertEquals("GET", apiRequests.get(0).method());
            assertEquals("/api/v1/box/items", apiRequests.get(0).path());
            assertEquals("POST", apiRequests.get(1).method());
            assertEquals("/api/v1/box/items", apiRequests.get(1).path());
            assertEquals("/api/v1/box/items/box-1", apiRequests.get(2).path());
            assertEquals("/api/v1/box/items/box-1/schedule", apiRequests.get(3).path());
            assertEquals("/api/v1/box/items/box-1/send-now", apiRequests.get(4).path());
            assertEquals("/api/v1/box/items/box-1/retry", apiRequests.get(5).path());
            assertEquals("/api/v1/box/items/box-1/cancel", apiRequests.get(6).path());
            assertEquals(true, apiRequests.get(1).body().contains("\"payloadXml\""));
            assertEquals(true, apiRequests.get(1).body().contains("Invoice"));
            assertEquals(true, apiRequests.get(1).body().contains("\"externalId\":\"erp-doc-1\""));
            assertEquals(true, apiRequests.get(3).body().contains("\"scheduledFor\":\"2026-07-01T00:00:00.000Z\""));
        }
    }

    private static List<NamedCall> v2Calls() {
        return List.of(
                new NamedCall("mapper", client -> client.connector().mapper(Map.of(
                        "templateKey", "pohoda-csv-v1",
                        "sourceType", "csv",
                        "sourceText", "Doklad"
                ))),
                new NamedCall("zenInput", client -> client.connector().zenInput(Map.of("customerRef", "cust-1"))),
                new NamedCall("autopilot", client -> client.connector().autopilot(new ConnectorAutopilotRequest(
                        "cust-1", "shadow", null, null, Map.of(), null, Map.of()
                ))),
                new NamedCall("getAutopilotRun", client -> client.connector().getAutopilotRun("run-1")),
                new NamedCall("sendAutopilotRun", client -> client.connector().sendAutopilotRun("run-1")),
                new NamedCall("reconcile", client -> client.connector().reconcile(new ConnectorReconcileParams("exceptions", null))),
                new NamedCall("mailboxes", client -> client.connector().mailboxes()),
                new NamedCall("repairMailbox", client -> client.connector().repairMailbox(new ConnectorMailboxRepairRequest("cust-1"))),
                new NamedCall("updateMailboxSendPolicy", client -> client.connector().updateMailboxSendPolicy(
                        "cust-1",
                        new ConnectorSendPolicyOptions("manual", null)
                )),
                new NamedCall("sync", client -> client.connector().sync(new ConnectorSyncParams("cust-1", null, null))),
                new NamedCall("getDocument", client -> client.connector().getDocument("doc-1")),
                new NamedCall("getDocumentUbl", client -> client.connector().getDocumentUbl("doc-1")),
                new NamedCall("getDocumentEvidence", client -> client.connector().getDocumentEvidence("doc-1")),
                new NamedCall("getDocumentEvidenceBundle", client -> client.connector().getDocumentEvidenceBundle("doc-1")),
                new NamedCall("runAction", client -> client.connector().runAction(
                        "action-1",
                        new ConnectorActionRequest(null, null, "approve")
                ))
        );
    }

    private static EPostak createClient(CaptureServer server) {
        return EPostak.builder()
                .clientId("sk_int_test")
                .clientSecret("sk_int_test")
                .baseUrl(server.baseUrl())
                .firmId("firm-123")
                .build();
    }

    private static CapturedRequest singleApiRequest(CaptureServer server) {
        List<CapturedRequest> apiRequests = server.requests().stream()
                .filter(request -> request.path().startsWith("/api/v1/connector/"))
                .toList();
        assertEquals(1, apiRequests.size());
        return apiRequests.get(0);
    }

    private static CapturedRequest singleNonAuthRequest(CaptureServer server) {
        List<CapturedRequest> apiRequests = server.requests().stream()
                .filter(request -> !request.path().equals("/sapi/v1/auth/token"))
                .toList();
        assertEquals(1, apiRequests.size());
        return apiRequests.get(0);
    }

    private record NamedCall(String name, ConnectorCall call) {
        void invoke(EPostak client) {
            call.invoke(client);
        }
    }

    @FunctionalInterface
    private interface ConnectorCall {
        void invoke(EPostak client);
    }

    private record CapturedRequest(
            String method,
            String path,
            String firmId,
            String participantId,
            String idempotencyKey,
            String body
    ) {}

    private static final class CaptureServer implements AutoCloseable {
        private final HttpServer server;
        private final List<CapturedRequest> requests = new ArrayList<>();

        private CaptureServer(HttpServer server) {
            this.server = server;
        }

        static CaptureServer start() throws IOException {
            HttpServer httpServer = HttpServer.create(new InetSocketAddress(InetAddress.getLoopbackAddress(), 0), 0);
            CaptureServer captureServer = new CaptureServer(httpServer);
            httpServer.createContext("/", captureServer::handle);
            httpServer.start();
            return captureServer;
        }

        String baseUrl() {
            return "http://" + server.getAddress().getHostString() + ":" + server.getAddress().getPort() + "/api/v1";
        }

        List<CapturedRequest> requests() {
            return requests;
        }

        private void handle(HttpExchange exchange) throws IOException {
            String body = new String(exchange.getRequestBody().readAllBytes(), StandardCharsets.UTF_8);

            String path = exchange.getRequestURI().getPath();
            Headers headers = exchange.getRequestHeaders();
            requests.add(new CapturedRequest(
                    exchange.getRequestMethod(),
                    path,
                    headers.getFirst("X-Firm-Id"),
                    headers.getFirst("X-Peppol-Participant-Id"),
                    headers.getFirst("Idempotency-Key"),
                    body
            ));

            if (path.equals("/sapi/v1/auth/token")) {
                respond(exchange, "application/json", "{\"access_token\":\"token\",\"refresh_token\":\"refresh\",\"expires_in\":3600}");
                return;
            }
            if (path.endsWith("/ubl")) {
                respond(exchange, "application/xml", "<xml/>");
                return;
            }
            respond(exchange, "application/json", "{}");
        }

        private static void respond(HttpExchange exchange, String contentType, String body) throws IOException {
            byte[] bytes = body.getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", contentType);
            exchange.sendResponseHeaders(200, bytes.length);
            exchange.getResponseBody().write(bytes);
            exchange.close();
        }

        @Override
        public void close() {
            server.stop(0);
        }
    }
}
