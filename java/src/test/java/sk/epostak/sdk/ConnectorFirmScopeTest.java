package sk.epostak.sdk;

import com.sun.net.httpserver.Headers;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;
import org.junit.jupiter.api.Test;
import sk.epostak.sdk.models.ConnectorActionRequest;
import sk.epostak.sdk.models.ConnectorAutopilotRequest;
import sk.epostak.sdk.models.ConnectorListParams;
import sk.epostak.sdk.models.ConnectorMailboxRepairRequest;
import sk.epostak.sdk.models.ConnectorPreflightRequest;
import sk.epostak.sdk.models.ConnectorReconcileParams;
import sk.epostak.sdk.models.ConnectorSendPolicyOptions;
import sk.epostak.sdk.models.ConnectorSyncParams;

import java.io.IOException;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;

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

    private static List<NamedCall> v2Calls() {
        return List.of(
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

    private record NamedCall(String name, ConnectorCall call) {
        void invoke(EPostak client) {
            call.invoke(client);
        }
    }

    @FunctionalInterface
    private interface ConnectorCall {
        void invoke(EPostak client);
    }

    private record CapturedRequest(String method, String path, String firmId) {}

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
            exchange.getRequestBody().readAllBytes();

            String path = exchange.getRequestURI().getPath();
            Headers headers = exchange.getRequestHeaders();
            requests.add(new CapturedRequest(
                    exchange.getRequestMethod(),
                    path,
                    headers.getFirst("X-Firm-Id")
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
