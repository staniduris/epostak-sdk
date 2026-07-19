package sk.epostak.sdk;

import com.google.gson.Gson;
import com.sun.net.httpserver.Headers;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;
import org.junit.jupiter.api.Test;
import sk.epostak.sdk.models.BoxCreateRequest;
import sk.epostak.sdk.models.BoxListParams;
import sk.epostak.sdk.models.BoxScheduleRequest;
import sk.epostak.sdk.models.ConnectorActionRequest;
import sk.epostak.sdk.models.ConnectorAutopilotRequest;
import sk.epostak.sdk.models.ConnectorBusinessLine;
import sk.epostak.sdk.models.ConnectorBusinessDocumentRequest;
import sk.epostak.sdk.models.ConnectorBusinessAttachment;
import sk.epostak.sdk.models.ConnectorBusinessAddress;
import sk.epostak.sdk.models.ConnectorBusinessDocument;
import sk.epostak.sdk.models.ConnectorBusinessDocumentListParams;
import sk.epostak.sdk.models.ConnectorBusinessEventsResponse;
import sk.epostak.sdk.models.ConnectorBusinessPrepayment;
import sk.epostak.sdk.models.ConnectorBusinessRecipient;
import sk.epostak.sdk.models.ConnectorEventsResponse;
import sk.epostak.sdk.models.ConnectorListParams;
import sk.epostak.sdk.models.ConnectorInvoiceResponseRequest;
import sk.epostak.sdk.models.ConnectorMailboxRepairRequest;
import sk.epostak.sdk.models.ConnectorPreflightRequest;
import sk.epostak.sdk.models.ConnectorReconcileParams;
import sk.epostak.sdk.models.ConnectorSendPolicyOptions;
import sk.epostak.sdk.models.ConnectorSubmitDocumentRequest;
import sk.epostak.sdk.models.ConnectorSyncParams;
import sk.epostak.sdk.models.CapabilitiesRequest;
import sk.epostak.sdk.models.SendDocumentRequest;
import sk.epostak.sdk.resources.ConnectorCustomerDocumentsResource;
import sk.epostak.sdk.resources.ConnectorCustomersResource;
import sk.epostak.sdk.resources.ConnectorResource;

import java.io.IOException;
import java.lang.invoke.MethodType;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.stream.Collectors;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
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

            client.connector().advanced().preflight(new ConnectorPreflightRequest(
                    "0245:1234567890",
                    Map.of()
            ));

            CapturedRequest request = singleApiRequest(server);
            assertEquals("firm-123", request.firmId());
        }
    }

    @Test
    void customerScopedConnectorEventsAreThePrimaryNoFirmFeed() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.connector().customers().forCustomer("erp-customer-1").events().list(
                    new ConnectorListParams("cursor-1", 25)
            );

            CapturedRequest request = singleApiRequest(server);
            assertEquals("/api/v1/connector/events", request.path());
            assertNull(request.firmId());
            assertEquals(true, request.rawQuery().contains("customerRef=erp-customer-1"));
            assertEquals(true, request.rawQuery().contains("cursor=cursor-1"));
        }
    }

    @Test
    void connectorWebhookIsGlobalAndNeverSendsFirmId() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            var currentWebhook = client.connector().webhook().get();
            var configuredWebhook = client.connector().webhook().configure(
                    " https://erp.example/epostak ",
                    List.of("document.received")
            );
            client.connector().webhook().rotateSecret();
            var testDelivery = client.connector().webhook().test("\u00A0\uFEFFerp-customer-1\uFEFF\u00A0");
            var deliveries = client.connector().webhook().deliveries("next", 25, "FAILED");
            client.connector().webhook().getDelivery("delivery-1");
            client.connector().webhook().replayDelivery("delivery-1", "replay-key", false);
            client.connector().webhook().runTestSuite("erp-customer-1", null, null, "suite-key");
            client.connector().webhook().getTestSuite("run-1");
            client.connector().webhook().delete();

            List<CapturedRequest> requests = server.requests().stream()
                    .filter(request -> request.path().startsWith("/api/v1/connector/webhook"))
                    .toList();
            assertEquals(List.of(
                    "/api/v1/connector/webhook",
                    "/api/v1/connector/webhook",
                    "/api/v1/connector/webhook/rotate-secret",
                    "/api/v1/connector/webhook/test",
                    "/api/v1/connector/webhook/deliveries",
                    "/api/v1/connector/webhook/deliveries/delivery-1",
                    "/api/v1/connector/webhook/deliveries/delivery-1/replay",
                    "/api/v1/connector/webhook/test-suite",
                    "/api/v1/connector/webhook/test-suite/run-1",
                    "/api/v1/connector/webhook"
            ), requests.stream().map(CapturedRequest::path).toList());
            assertEquals(List.of("GET", "PUT", "POST", "POST", "GET", "GET", "POST", "POST", "GET", "DELETE"),
                    requests.stream().map(CapturedRequest::method).toList());
            assertEquals(true, requests.stream().allMatch(request -> request.firmId() == null));
            assertEquals(true, requests.get(1).body().contains("https://erp.example/epostak"));
            assertEquals(true, requests.get(3).body().contains("\"customerRef\":\"erp-customer-1\""));
            assertEquals("cursor=next&limit=25&status=FAILED", requests.get(4).rawQuery());
            assertEquals("replay-key", requests.get(6).idempotencyKey());
            assertEquals("suite-key", requests.get(7).idempotencyKey());
            assertEquals("wh-1", currentWebhook.webhook().id());
            assertEquals("a".repeat(64), configuredWebhook.secret());
            assertEquals("whd-1", testDelivery.deliveryId());
            assertEquals(true, testDelivery.event().test());
            assertEquals("erp-customer-1", testDelivery.event().customerRef());
            assertNull(testDelivery.event().data().response());
            assertEquals(1, deliveries.deliveries().size());
            assertEquals("erp-customer-1", deliveries.deliveries().get(0).customerRef());
            assertThrows(IllegalArgumentException.class, () -> client.connector().webhook().configure(" "));
            assertThrows(IllegalArgumentException.class, () -> client.connector().webhook().test(" "));
        }
    }

    @Test
    void connectorAdvancedSurfaceOwnsLegacyPaths() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.connector().advanced().preflight(new ConnectorPreflightRequest("0245:1234567890", Map.of()));
            client.connector().advanced().send(Map.of("payload", Map.of()), "send-key");
            client.connector().advanced().status("doc-1");
            client.connector().advanced().inbox();
            client.connector().advanced().getInboxDocument("doc-1");
            client.connector().advanced().ack("doc-1");
            client.connector().advanced().listOutbox();
            client.connector().advanced().getOutboxItem("out-1");
            client.connector().advanced().sendOutboxItem("out-1");
            client.connector().advanced().sendOutboxBatch();
            client.connector().advanced().cancelOutboxItem("out-1");

            List<CapturedRequest> requests = server.requests().stream()
                    .filter(request -> request.path().startsWith("/api/v1/connector/"))
                    .toList();
            assertEquals(List.of(
                    "/api/v1/connector/preflight",
                    "/api/v1/connector/send",
                    "/api/v1/connector/status/doc-1",
                    "/api/v1/connector/inbox",
                    "/api/v1/connector/inbox/doc-1",
                    "/api/v1/connector/inbox/doc-1/ack",
                    "/api/v1/connector/outbox",
                    "/api/v1/connector/outbox/out-1",
                    "/api/v1/connector/outbox/out-1/send",
                    "/api/v1/connector/outbox/send",
                    "/api/v1/connector/outbox/out-1"
            ), requests.stream().map(CapturedRequest::path).toList());
            assertEquals(true, requests.stream().allMatch(request -> "firm-123".equals(request.firmId())));
            assertEquals("send-key", requests.get(1).idempotencyKey());
        }
    }

    @Test
    void connectorCompatibilityAliasesStaySilentAndCustomersCannotBeCreated() throws Exception {
        assertNotNull(ConnectorResource.class
                .getMethod("preflight", ConnectorPreflightRequest.class));

        Set<String> customerMethods = Set.of(ConnectorCustomersResource.class.getDeclaredMethods()).stream()
                .filter(method -> java.lang.reflect.Modifier.isPublic(method.getModifiers()))
                .map(java.lang.reflect.Method::getName)
                .collect(Collectors.toSet());
        assertEquals(Set.of("forCustomer"), customerMethods);
        assertThrows(IllegalArgumentException.class, () -> newClientWithoutRequests().connector().customers().forCustomer(" "));
    }

    private static EPostak newClientWithoutRequests() {
        return EPostak.builder()
                .clientId("sk_int_test")
                .clientSecret("sk_int_test")
                .baseUrl("http://127.0.0.1:1/api/v1")
                .build();
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
    void customerScopedConnectorSubmitDocumentOmitsFirmIdAndDefaultsToSend() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.connector().customers().forCustomer("erp-customer-1").documents().send(
                    new ConnectorBusinessDocumentRequest(
                            "FA-1",
                            "FA-1",
                            ConnectorBusinessRecipient.byTaxId("SK", "2120123456"),
                            List.of(new ConnectorBusinessLine("Licence", 1, 100, 23))
                    )
            );

            CapturedRequest request = singleApiRequest(server);
            assertEquals("/api/v1/connector/documents", request.path());
            assertEquals(null, request.firmId());
            assertEquals(
                    "connector:v1:f7be06badbccd0670a25e6df7fd654fd45ae7291d5f5043257806adc0b107045",
                    request.idempotencyKey()
            );
            assertEquals(77, request.idempotencyKey().length());
            assertEquals(true, request.body().contains("\"customerRef\":\"erp-customer-1\""));
            assertEquals(true, request.body().contains("\"delivery\":\"send\""));
            assertEquals(false, request.body().toLowerCase().contains("peppol"));
        }
    }

    @Test
    void customerStageFilteredListAndInvoiceResponseUseCanonicalWireContract() throws Exception {
        String responseJson = "{\"id\":\"doc-in-1\",\"customerRef\":\"erp-customer-1\",\"response\":{\"status\":\"accepted\",\"direction\":\"sent\",\"delivery\":\"queued\",\"respondedAt\":\"2026-07-15T12:00:00Z\"},\"idempotent\":true}";
        try (ErrorServer server = ErrorServer.startSequence(List.of(
                new ErrorSpec(201, "{\"id\":\"doc-stage-1\",\"state\":\"queued\"}", Map.of()),
                new ErrorSpec(200, "{\"documents\":[],\"nextCursor\":\"cur-2\",\"hasMore\":true}", Map.of()),
                new ErrorSpec(200, responseJson, Map.of())
        ))) {
            EPostak client = createClient(server.baseUrl());
            var documents = client.connector().customers().forCustomer("erp-customer-1").documents();
            ConnectorBusinessDocumentRequest request = new ConnectorBusinessDocumentRequest(
                    "FA-STAGE-1",
                    "FA-STAGE-1",
                    ConnectorBusinessRecipient.byTaxId("SK", "2120123456"),
                    List.of(new ConnectorBusinessLine("Licence", 1, 100, 23))
            );

            documents.stage(request, "connector-stage-key");
            var page = documents.list(new ConnectorBusinessDocumentListParams(
                    "inbound",
                    "received",
                    "invoice",
                    "2026-07-01T00:00:00Z",
                    "cur-1",
                    25
            ));
            var result = documents.respond(
                    "doc-in-1",
                    new ConnectorInvoiceResponseRequest("accepted", "Imported into ERP")
            );

            assertEquals(3, server.requests().size());
            CapturedRequest stage = server.requests().get(0);
            CapturedRequest list = server.requests().get(1);
            CapturedRequest respond = server.requests().get(2);
            assertEquals("POST", stage.method());
            assertEquals("/api/v1/connector/documents", stage.path());
            assertEquals("connector-stage-key", stage.idempotencyKey());
            assertNull(stage.firmId());
            Map<?, ?> stageBody = new Gson().fromJson(stage.body(), Map.class);
            assertEquals("erp-customer-1", stageBody.get("customerRef"));
            assertEquals("stage", stageBody.get("delivery"));
            assertEquals("GET", list.method());
            assertEquals("/api/v1/connector/documents", list.path());
            assertEquals(
                    "customerRef=erp-customer-1&direction=inbound&state=received&type=invoice&createdAfter=2026-07-01T00%3A00%3A00Z&cursor=cur-1&limit=25",
                    list.rawQuery()
            );
            assertNull(list.firmId());
            assertEquals("POST", respond.method());
            assertEquals("/api/v1/connector/documents/doc-in-1/respond", respond.path());
            assertEquals("customerRef=erp-customer-1", respond.rawQuery());
            assertNull(respond.firmId());
            assertNull(respond.idempotencyKey());
            assertEquals(
                    Map.of("status", "accepted", "note", "Imported into ERP"),
                    new Gson().fromJson(respond.body(), Map.class)
            );
            assertEquals("cur-2", page.nextCursor());
            assertEquals(true, page.hasMore());
            assertEquals("accepted", result.response().status());
            assertEquals("sent", result.response().direction());
            assertEquals("queued", result.response().delivery());
            assertEquals(true, result.idempotent());
        }

        try (ErrorServer server = ErrorServer.startSequence(List.of(
                new ErrorSpec(503, "{\"error\":{\"code\":\"temporary\",\"message\":\"retry\"}}", Map.of("Retry-After", "0")),
                new ErrorSpec(200, responseJson, Map.of())
        ))) {
            var result = createClient(server.baseUrl()).connector().customers()
                    .forCustomer("erp-customer-1").documents()
                    .respond("doc-in-1", new ConnectorInvoiceResponseRequest("accepted", "Imported into ERP"));
            assertEquals(2, server.requests().size());
            assertEquals(server.requests().get(0).path(), server.requests().get(1).path());
            assertEquals(server.requests().get(0).rawQuery(), server.requests().get(1).rawQuery());
            assertEquals(server.requests().get(0).body(), server.requests().get(1).body());
            assertEquals(true, server.requests().stream().allMatch(request ->
                    request.firmId() == null && request.idempotencyKey() == null
            ));
            assertEquals(true, result.idempotent());
        }
    }

    @Test
    void customerMapperIsPreviewOnly() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.connector().customers().forCustomer("erp-customer-1").advanced().mapper(Map.of(
                    "templateKey", "pohoda-csv-v1",
                    "sourceType", "csv",
                    "sourceText", "Doklad"
            ));

            CapturedRequest request = singleApiRequest(server);
            assertEquals("/api/v1/connector/mapper", request.path());
            assertEquals(true, request.body().contains("\"customerRef\":\"erp-customer-1\""));
            assertEquals(true, request.body().contains("\"execute\":\"preview\""));
        }

        assertThrows(IllegalArgumentException.class, () ->
                newClientWithoutRequests().connector().customers().forCustomer("customer")
                        .advanced().mapper(Map.of("execute", "send")));
    }

    @Test
    void compatibilitySubmitAliasKeepsAutopilotStageSemanticsAndSourceShape() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);
            ConnectorSubmitDocumentRequest request = new ConnectorSubmitDocumentRequest(
                    "legacy-1",
                    "legacy-key",
                    Map.of("invoiceNumber", "FA-1")
            );

            client.connector().customers().forCustomer("erp-customer-1").submitDocument(request);

            CapturedRequest sent = singleApiRequest(server);
            assertEquals("/api/v1/connector/autopilot", sent.path());
            assertEquals(true, sent.body().contains("\"customerRef\":\"erp-customer-1\""));
            assertEquals(true, sent.body().contains("\"mode\":\"stage\""));
            assertNull(request.customerRef());
            assertNull(request.mode());
            assertNotNull(ConnectorResource.class
                    .getMethod("submitDocument", ConnectorSubmitDocumentRequest.class));
        }
    }

    @Test
    void legacyAndBusinessEventFeedsKeepDistinctResponseTypesAndScopes() throws Exception {
        String businessJson = """
                {"events":[{"id":"evt-1","customerRef":"erp-customer-1","documentId":"11111111-1111-1111-1111-111111111111","type":"document.cancelled","state":"cancelled","occurredAt":"2026-07-14T10:00:00Z","data":{"customerRef":"erp-customer-1","direction":"outbound","type":"invoice","number":null,"response":null}}],"nextCursor":null,"hasMore":false}
                """;
        String legacyJson = """
                {"events":[{"id":"legacy-1","documentId":"11111111-1111-1111-1111-111111111111","type":"delivery.updated","status":"DELIVERED","occurredAt":"2026-07-14T10:00:00Z","data":{"transport":"as4"}}],"nextCursor":null,"hasMore":false}
                """;
        Gson gson = new Gson();

        ConnectorBusinessEventsResponse business = gson.fromJson(businessJson, ConnectorBusinessEventsResponse.class);
        ConnectorEventsResponse legacy = gson.fromJson(legacyJson, ConnectorEventsResponse.class);

        assertEquals("document.cancelled", business.events().get(0).type());
        assertEquals("cancelled", business.events().get(0).state());
        assertEquals("erp-customer-1", business.events().get(0).customerRef());
        assertEquals("erp-customer-1", business.events().get(0).data().get("customerRef"));
        assertEquals(true, business.events().get(0).data().containsKey("number"));
        assertEquals(true, business.events().get(0).data().containsKey("response"));
        assertNull(business.events().get(0).data().get("response"));
        assertEquals("DELIVERED", legacy.events().get(0).status());

        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);
            client.connector().customers().forCustomer("erp-customer-1").events().list();
            client.connector().advanced().events();
            List<CapturedRequest> requests = server.requests().stream()
                    .filter(request -> request.path().equals("/api/v1/connector/events"))
                    .toList();
            assertNull(requests.get(0).firmId());
            assertEquals(true, requests.get(0).rawQuery().contains("customerRef=erp-customer-1"));
            assertEquals("firm-123", requests.get(1).firmId());
            assertNull(requests.get(1).rawQuery());
        }
    }

    @Test
    void connectorBusinessModelsPreserveCanonicalRequestAndResponseFields() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);
            ConnectorBusinessDocumentRequest request = new ConnectorBusinessDocumentRequest(
                    "FA-1",
                    "FA-1",
                    new ConnectorBusinessRecipient(
                            "SK", "Buyer", null, "2120123456", null, null,
                            new ConnectorBusinessAddress("Hlavna 1", "Bratislava", "81101")
                    ),
                    List.of(new ConnectorBusinessLine(
                            "Licence", 1, 100, 23, "C62", "standard", 5.0,
                            "2026-07-14", "service", "ADV-1", "9983", "CC-1",
                            "UNSPSC", null, "A1", 1.0, "C62"
                    ))
            ).buyerReference("PO-7")
                    .prepaidAmount(50.0)
                    .prepayments(List.of(new ConnectorBusinessPrepayment(
                            "ADV-1", "TAX-1", "2026-07-01", 40.65, 9.35, 50, 23.0, "standard"
                    )))
                    .attachments(List.of(new ConnectorBusinessAttachment(
                            "terms.pdf", "application/pdf", "YQ==", "Terms"
                    )));

            client.connector().customers().forCustomer("erp-customer-1").documents().send(request);
            String body = singleApiRequest(server).body();
            assertEquals(true, body.contains("\"buyerReference\":\"PO-7\""));
            assertEquals(true, body.contains("\"address\""));
            assertEquals(true, body.contains("\"discount\":5.0"));
            assertEquals(true, body.contains("\"deliveryDate\":\"2026-07-14\""));
            assertEquals(true, body.contains("\"prepayments\""));
            assertEquals(true, body.contains("\"attachments\""));
        }

        ConnectorBusinessDocument response = new Gson().fromJson("""
                {"id":"11111111-1111-1111-1111-111111111111","customerRef":"erp-customer-1","externalId":"FA-1","direction":"outbound","type":"invoice","number":"FA-1","state":"queued","replayed":false,"currency":"EUR","amounts":{"withoutTax":100,"tax":23,"total":123,"due":73},"sender":{"name":"Sender","country":"SK","companyId":"12345678","resolution":"verified"},"recipient":{"name":"Buyer","country":"SK","taxId":"2120123456","resolution":"verified"},"issueDate":"2026-07-14","dueDate":"2026-07-28","processedAt":"2026-07-14T10:00:00Z","processedReference":"ERP-OK","createdAt":"2026-07-14T09:00:00Z","updatedAt":"2026-07-14T10:00:00Z","response":{"status":"accepted","direction":"sent","reason":"Approved","respondedAt":"2026-07-15T12:00:00Z"},"links":{"self":"/connector/documents/1"}}
                """, ConnectorBusinessDocument.class);
        assertEquals(73.0, response.amounts().due());
        assertEquals("Sender", response.sender().name());
        assertEquals("2026-07-14", response.issueDate());
        assertEquals("ERP-OK", response.processedReference());
        assertEquals("accepted", response.response().status());
        assertEquals("sent", response.response().direction());
        assertEquals("Approved", response.response().reason());
        assertEquals("2026-07-15T12:00:00Z", response.response().respondedAt());
    }

    @Test
    void advancedDocumentArtifactsHaveCanonicalCustomerHomeAndCompatibilityAliases() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);
            var customer = client.connector().customers().forCustomer("erp-customer-1");

            customer.advanced().documents().ubl("doc-1");
            client.connector().advanced().documents().evidence("doc-1");

            List<String> paths = server.requests().stream()
                    .filter(request -> request.path().startsWith("/api/v1/connector/documents/"))
                    .map(CapturedRequest::path)
                    .toList();
            assertEquals(List.of(
                    "/api/v1/connector/documents/doc-1/ubl",
                    "/api/v1/connector/documents/doc-1/evidence"
            ), paths);
            assertNotNull(customer.documents().getClass()
                    .getMethod("ubl", String.class));
        }
    }

    @Test
    void defaultIdempotencyKeySeparatesDelimiterCollisionPairs() throws Exception {
        CapturedRequest first = submitConnectorDocument("a:b", "c", null);
        CapturedRequest second = submitConnectorDocument("a", "b:c", null);

        assertEquals(
                "connector:v1:540e8f1c5ae653a7d7e2fe88f7eb8dcabea924d661b1542ad191bb1848e0c33d",
                first.idempotencyKey()
        );
        assertEquals(
                "connector:v1:e482a79a788392ccae4952360dd438820641e4c162b4952b42d35e78260d70be",
                second.idempotencyKey()
        );
        assertEquals(false, first.idempotencyKey().equals(second.idempotencyKey()));
    }

    @Test
    void defaultIdempotencyKeySupportsMaximumInputLengths() throws Exception {
        CapturedRequest request = submitConnectorDocument("c".repeat(255), "e".repeat(255), null);

        assertEquals(
                "connector:v1:7182fd43682e0689adf34c908bc3ec162aaf1687c167fdbff714ff43daa4b111",
                request.idempotencyKey()
        );
        assertEquals(77, request.idempotencyKey().length());
    }

    @Test
    void defaultIdempotencyKeyNormalizesWhitespaceAndPreservesUnicode() throws Exception {
        CapturedRequest request = submitConnectorDocument("\u00A0\uFEFFzákazník😀\uFEFF\u00A0", "\uFEFFFA-žltý-1\u00A0", null);

        assertEquals(
                "connector:v1:eec0ca654af898913432fbc7b7441a05080f72099f6d2ff85852f78c7458fdfd",
                request.idempotencyKey()
        );
        Map<?, ?> body = new Gson().fromJson(request.body(), Map.class);
        assertEquals("zákazník😀", body.get("customerRef"));
        assertEquals("FA-žltý-1", body.get("externalId"));
    }

    @Test
    void defaultIdempotencyKeyDoesNotTrimC1NextLineControl() throws Exception {
        CapturedRequest request = submitConnectorDocument("\u0085zákazník😀\u0085", "\u0085FA-žltý-1\u0085", null);
        assertEquals(
                "connector:v1:ff49689a9ece4c0319420ed07fc3a2a5b2e2e7bb6d4430a68557e372fdf70080",
                request.idempotencyKey()
        );
        Map<?, ?> body = new Gson().fromJson(request.body(), Map.class);
        assertEquals("\u0085zákazník😀\u0085", body.get("customerRef"));
        assertEquals("\u0085FA-žltý-1\u0085", body.get("externalId"));
    }

    @Test
    void explicitConnectorIdempotencyKeyIsPreserved() throws Exception {
        CapturedRequest request = submitConnectorDocument("erp-customer-1", "FA-1", "erp-retry-key");

        assertEquals("erp-retry-key", request.idempotencyKey());
        assertThrows(
                IllegalArgumentException.class,
                () -> submitConnectorDocument("customer", "empty-key", "")
        );
    }

    @Test
    void connectorRetriesKeyedAndLifecyclePostsButNotConflict() throws Exception {
        try (ErrorServer server = ErrorServer.startSequence(List.of(
                new ErrorSpec(503, "{\"error\":{\"code\":\"temporary\",\"message\":\"retry\"}}", Map.of("Retry-After", "0")),
                new ErrorSpec(201, "{\"id\":\"doc-1\",\"state\":\"queued\"}", Map.of())
        ))) {
            EPostak client = createClient(server.baseUrl());
            ConnectorBusinessDocumentRequest request = new ConnectorBusinessDocumentRequest(
                    "FA-retry",
                    "FA-retry",
                    ConnectorBusinessRecipient.byTaxId("SK", "2120123456"),
                    List.of(new ConnectorBusinessLine("Original", 1, 1, 23))
            );
            client.connector().customers().forCustomer("customer").documents().send(request, null);
            assertEquals(2, server.requests().size());
            assertEquals(server.requests().get(0).body(), server.requests().get(1).body());
            assertEquals(server.requests().get(0).idempotencyKey(), server.requests().get(1).idempotencyKey());
        }

        try (ErrorServer server = ErrorServer.startSequence(List.of(
                new ErrorSpec(503, "{}", Map.of("Retry-After", "0")),
                new ErrorSpec(200, "{\"id\":\"doc-1\",\"state\":\"cancelled\"}", Map.of())
        ))) {
            createClient(server.baseUrl()).connector().customers().forCustomer("customer")
                    .documents().cancelDocument("doc-1");
            assertEquals(2, server.requests().size());
        }

        try (ErrorServer server = ErrorServer.startSequence(List.of(
                new ErrorSpec(503, "{}", Map.of("Retry-After", "0")),
                new ErrorSpec(200, "<Invoice/>", Map.of())
        ))) {
            String ubl = createClient(server.baseUrl()).connector().customers().forCustomer("customer")
                    .advanced().documents().ubl("doc-1");
            assertEquals("<Invoice/>", ubl);
            assertEquals(2, server.requests().size());
            assertEquals(true, server.requests().stream().allMatch(request ->
                    "/api/v1/connector/documents/doc-1/ubl".equals(request.path())
                            && "customerRef=customer".equals(request.rawQuery())
                            && request.firmId() == null
            ));
        }

        try (ErrorServer server = ErrorServer.startSequence(List.of(
                new ErrorSpec(409, "{\"error\":{\"code\":\"idempotency_in_flight\",\"message\":\"busy\",\"retryable\":true}}", Map.of("Retry-After", "0"))
        ))) {
            EPostak client = createClient(server.baseUrl());
            ConnectorBusinessDocumentRequest request = new ConnectorBusinessDocumentRequest(
                    "FA-conflict",
                    "FA-conflict",
                    ConnectorBusinessRecipient.byTaxId("SK", "2120123456"),
                    List.of(new ConnectorBusinessLine("Original", 1, 1, 23))
            );
            EPostakException conflict = assertThrows(
                    EPostakException.class,
                    () -> client.connector().customers().forCustomer("customer").documents().send(request, null)
            );
            assertEquals(409, conflict.getStatus());
            assertEquals(1, server.requests().size());
        }
    }

    @Test
    void connectorRetriesTransportDropButSapiMutationSurfacesOnce() throws Exception {
        try (TransportDropServer server = TransportDropServer.start(true)) {
            EPostak client = createClient(server.baseUrl());
            ConnectorBusinessDocumentRequest request = new ConnectorBusinessDocumentRequest(
                    "FA-TRANSPORT-1",
                    "FA-TRANSPORT-1",
                    ConnectorBusinessRecipient.byTaxId("SK", "2120123456"),
                    List.of(new ConnectorBusinessLine("Licence", 1, 100, 23))
            );

            client.connector().customers().forCustomer("erp-customer-1").documents()
                    .stage(request, "connector-transport-key");

            assertEquals(2, server.requests().size());
            CapturedRequest first = server.requests().get(0);
            CapturedRequest second = server.requests().get(1);
            assertEquals("POST", first.method());
            assertEquals("/api/v1/connector/documents", first.path());
            assertEquals(first.path(), second.path());
            assertEquals(first.rawQuery(), second.rawQuery());
            assertEquals(first.body(), second.body());
            assertEquals(first.idempotencyKey(), second.idempotencyKey());
            assertEquals("connector-transport-key", first.idempotencyKey());
            assertNull(first.firmId());
            assertNull(second.firmId());
            assertEquals(true, first.body().contains("\"customerRef\":\"erp-customer-1\""));
            assertEquals(true, first.body().contains("\"delivery\":\"stage\""));
        }

        try (TransportDropServer server = TransportDropServer.start(false)) {
            EPostakException error = assertThrows(
                    EPostakException.class,
                    () -> createClient(server.baseUrl()).sapi().participants()
                            .forParticipant("0245:1234567890")
                            .documents()
                            .send(Map.of("xml", "<Invoice/>"), "sapi-transport-key")
            );
            assertEquals(0, error.getStatus());
            assertEquals(1, server.requests().size());
        }
    }

    @Test
    void connectorRetryOptInDoesNotChangeSapiSendBehavior() throws Exception {
        try (ErrorServer server = ErrorServer.startSequence(List.of(
                new ErrorSpec(503, "{\"error\":{\"code\":\"temporary\",\"message\":\"retry\"}}", Map.of("Retry-After", "0")),
                new ErrorSpec(200, "{}", Map.of())
        ))) {
            EPostakException error = assertThrows(
                    EPostakException.class,
                    () -> createClient(server.baseUrl()).sapi().participants()
                            .forParticipant("0245:1234567890")
                            .documents()
                            .send(Map.of("xml", "<Invoice/>"), "sapi-key")
            );
            assertEquals(503, error.getStatus());
            assertEquals(1, server.requests().size());
        }
    }

    @Test
    void apiErrorsPreserveBusinessRetryMetadataAndRetryAfterSeconds() throws Exception {
        try (ErrorServer server = ErrorServer.start(
                409,
                "{\"error\":{\"code\":\"idempotency_in_flight\",\"message\":\"Still processing\",\"field\":\"externalId\",\"nextAction\":\"retry\",\"retryable\":true,\"requestId\":\"req-body\"}}",
                Map.of("Retry-After", "7", "X-Request-Id", "req-header"))) {
            EPostakException retryable = assertThrows(
                    EPostakException.class,
                    () -> createClient(server.baseUrl()).connector().customers()
                            .forCustomer("customer").documents().get("doc-1")
            );
            assertEquals("externalId", retryable.getField());
            assertEquals("retry", retryable.getNextAction());
            assertEquals(true, retryable.isRetryable());
            assertEquals("req-body", retryable.getRequestId());
            assertEquals(7, retryable.getRetryAfter());
        }

        try (ErrorServer server = ErrorServer.start(
                422,
                "{\"error\":{\"code\":\"validation_failed\",\"message\":\"Fix request\",\"retryable\":false}}",
                Map.of())) {
            EPostakException validation = assertThrows(
                    EPostakException.class,
                    () -> createClient(server.baseUrl()).connector().customers()
                            .forCustomer("customer").documents().get("doc-2")
            );
            assertEquals(false, validation.isRetryable());
            assertNull(validation.getRetryAfter());
        }
    }

    @Test
    void customerDocumentsKeepLegacyGetAbiAndExposeTypedBusinessDetail() throws Exception {
        var legacyMethod = ConnectorCustomerDocumentsResource.class.getMethod("get", String.class);
        assertEquals(Map.class, legacyMethod.getReturnType());
        assertEquals(
                "(Ljava/lang/String;)Ljava/util/Map;",
                MethodType.methodType(legacyMethod.getReturnType(), legacyMethod.getParameterTypes())
                        .toMethodDescriptorString()
        );
        assertEquals(
                ConnectorBusinessDocument.class,
                ConnectorCustomerDocumentsResource.class
                        .getMethod("getBusinessDocument", String.class)
                        .getReturnType()
        );

        try (ErrorServer server = ErrorServer.startSequence(List.of(
                new ErrorSpec(200, "{\"id\":\"legacy-detail\",\"state\":\"received\"}", Map.of()),
                new ErrorSpec(200, "{\"id\":\"typed-detail\",\"state\":\"received\"}", Map.of())
        ))) {
            ConnectorCustomerDocumentsResource documents = createClient(server.baseUrl())
                    .connector().customers().forCustomer("customer A/1").documents();

            // This assignment is the pre-existing consumer source contract.
            Map<String, Object> legacy = documents.get("legacy-detail");
            ConnectorBusinessDocument typed = documents.getBusinessDocument("typed-detail");

            assertEquals("legacy-detail", legacy.get("id"));
            assertEquals("typed-detail", typed.id());
            assertEquals(
                    List.of("customerRef=customer+A%2F1", "customerRef=customer+A%2F1"),
                    server.requests().stream().map(CapturedRequest::rawQuery).toList()
            );
            assertEquals(true, server.requests().stream().allMatch(request -> request.firmId() == null));
        }
    }

    @Test
    void customerScopedDocumentLifecycleSendAndCancelUseNoBody() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);
            var documents = client.connector().customers().forCustomer("erp-customer-1").documents();

            documents.sendDocument("doc-1");
            documents.cancelDocument("doc-2");

            List<CapturedRequest> requests = server.requests().stream()
                    .filter(request -> request.path().startsWith("/api/v1/connector/"))
                    .toList();
            assertEquals(List.of(
                    "/api/v1/connector/documents/doc-1/send",
                    "/api/v1/connector/documents/doc-2/cancel"
            ), requests.stream().map(CapturedRequest::path).toList());
            assertEquals(List.of(
                    "customerRef=erp-customer-1",
                    "customerRef=erp-customer-1"
            ), requests.stream().map(CapturedRequest::rawQuery).toList());
            assertEquals(true, requests.stream().allMatch(request -> request.body().isEmpty()));
            assertEquals(true, requests.stream().allMatch(request -> request.firmId() == null));
            assertThrows(IllegalArgumentException.class, () -> documents.sendDocument(" "));
            assertEquals(2, server.requests().stream()
                    .filter(request -> request.path().startsWith("/api/v1/connector/"))
                    .count());
        }
    }

    @Test
    void customerPointOperationsAndArtifactsAlwaysBindEncodedCustomerRef() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);
            var customer = client.connector().customers().forCustomer("customer A/1");
            String documentId = "customer-b-doc";

            customer.documents().get(documentId);
            customer.documents().acknowledge(documentId, "ERP-ACK-1");
            customer.documents().sendDocument(documentId);
            customer.documents().cancelDocument(documentId);
            customer.advanced().documents().ubl(documentId);
            customer.advanced().documents().evidence(documentId);
            customer.advanced().documents().evidenceBundle(documentId);
            customer.advanced().documents().supportPacket(documentId);

            List<CapturedRequest> requests = server.requests().stream()
                    .filter(request -> request.path().startsWith("/api/v1/connector/documents/"))
                    .toList();
            assertEquals(List.of(
                    "/api/v1/connector/documents/customer-b-doc",
                    "/api/v1/connector/documents/customer-b-doc/acknowledge",
                    "/api/v1/connector/documents/customer-b-doc/send",
                    "/api/v1/connector/documents/customer-b-doc/cancel",
                    "/api/v1/connector/documents/customer-b-doc/ubl",
                    "/api/v1/connector/documents/customer-b-doc/evidence",
                    "/api/v1/connector/documents/customer-b-doc/evidence-bundle",
                    "/api/v1/connector/documents/customer-b-doc/support-packet"
            ), requests.stream().map(CapturedRequest::path).toList());
            assertEquals(true, requests.stream().allMatch(
                    request -> "customerRef=customer+A%2F1".equals(request.rawQuery())
                            && request.firmId() == null
            ));
            assertEquals("{\"reference\":\"ERP-ACK-1\"}", requests.get(1).body());
            assertEquals(true, requests.get(2).body().isEmpty());
            assertEquals(true, requests.get(3).body().isEmpty());
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
    void jsonModeDocumentsSendAcceptsSelfBillingSupplierIdentity() throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);

            client.documents().send(
                    SendDocumentRequest.builder()
                            .documentType("self_billing")
                            .supplierPeppolId("0245:2123038963")
                            .supplierName("Dodavatel s.r.o.")
                            .items(List.of(new SendDocumentRequest.LineItem("Item", 1, 100, 23)))
                            .build()
            );

            CapturedRequest request = singleNonAuthRequest(server);
            assertEquals(true, request.body().contains("\"supplierPeppolId\":\"0245:2123038963\""));
            assertEquals(false, request.body().contains("receiverPeppolId"));
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
                new NamedCall("mapper", client -> client.connector().advanced().mapper(Map.of(
                        "templateKey", "pohoda-csv-v1",
                        "sourceType", "csv",
                        "sourceText", "Doklad"
                ))),
                new NamedCall("zenInput", client -> client.connector().advanced().zenInput(Map.of("customerRef", "cust-1"))),
                new NamedCall("autopilot", client -> client.connector().advanced().autopilot(new ConnectorAutopilotRequest(
                        "cust-1", "shadow", null, null, Map.of(), null, Map.of()
                ))),
                new NamedCall("getAutopilotRun", client -> client.connector().advanced().getAutopilotRun("run-1")),
                new NamedCall("sendAutopilotRun", client -> client.connector().advanced().sendAutopilotRun("run-1")),
                new NamedCall("reconcile", client -> client.connector().advanced().reconcile(new ConnectorReconcileParams("exceptions", null))),
                new NamedCall("mailboxes", client -> client.connector().advanced().mailboxes()),
                new NamedCall("repairMailbox", client -> client.connector().advanced().repairMailbox(new ConnectorMailboxRepairRequest("cust-1"))),
                new NamedCall("updateMailboxSendPolicy", client -> client.connector().advanced().updateMailboxSendPolicy(
                        "cust-1",
                        new ConnectorSendPolicyOptions("manual", null)
                )),
                new NamedCall("sync", client -> client.connector().advanced().sync(new ConnectorSyncParams("cust-1", null, null))),
                new NamedCall("getDocument", client -> client.connector().getDocument("doc-1")),
                new NamedCall("getDocumentUbl", client -> client.connector().getDocumentUbl("doc-1")),
                new NamedCall("getDocumentEvidence", client -> client.connector().getDocumentEvidence("doc-1")),
                new NamedCall("getDocumentEvidenceBundle", client -> client.connector().getDocumentEvidenceBundle("doc-1")),
                new NamedCall("runAction", client -> client.connector().advanced().runAction(
                        "action-1",
                        new ConnectorActionRequest(null, null, "approve")
                ))
        );
    }

    private static EPostak createClient(CaptureServer server) {
        return createClient(server.baseUrl());
    }

    private static EPostak createClient(String baseUrl) {
        return EPostak.builder()
                .clientId("sk_int_test")
                .clientSecret("sk_int_test")
                .baseUrl(baseUrl)
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

    private static CapturedRequest submitConnectorDocument(
            String customerRef,
            String externalId,
            String idempotencyKey
    ) throws Exception {
        try (CaptureServer server = CaptureServer.start()) {
            EPostak client = createClient(server);
            ConnectorBusinessDocumentRequest request = new ConnectorBusinessDocumentRequest(
                    externalId,
                    externalId,
                    ConnectorBusinessRecipient.byTaxId("SK", "2120123456"),
                    List.of(new ConnectorBusinessLine("Licence", 1, 100, 23))
            );
            client.connector().customers().forCustomer(customerRef).documents().send(request, idempotencyKey);
            return singleApiRequest(server);
        }
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
            String rawQuery,
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
                    exchange.getRequestURI().getRawQuery(),
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
            if (path.equals("/api/v1/connector/webhook/test")) {
                respond(exchange, "application/json", "{\"deliveryId\":\"whd-1\",\"status\":\"queued\",\"event\":{\"id\":\"evt-1\",\"customerRef\":\"erp-customer-1\",\"documentId\":\"doc-1\",\"type\":\"document.received\",\"state\":\"received\",\"occurredAt\":\"2026-07-15T10:00:00Z\",\"data\":{\"customerRef\":\"erp-customer-1\",\"direction\":\"inbound\",\"type\":\"invoice\",\"number\":null,\"response\":null},\"test\":true}}");
                return;
            }
            if (path.equals("/api/v1/connector/webhook/deliveries")) {
                respond(exchange, "application/json", "{\"deliveries\":[{\"id\":\"whd-1\",\"webhookId\":\"wh-1\",\"eventId\":\"evt-1\",\"customerRef\":\"erp-customer-1\",\"type\":\"document.received\",\"status\":\"SUCCESS\",\"attempts\":1,\"responseStatus\":204,\"responseTimeMs\":83,\"lastAttemptAt\":\"2026-07-15T10:00:01Z\",\"nextRetryAt\":null,\"createdAt\":\"2026-07-15T10:00:00Z\"}],\"nextCursor\":null,\"hasMore\":false}");
                return;
            }
            if (path.equals("/api/v1/connector/webhook") && !exchange.getRequestMethod().equals("DELETE")) {
                String secret = exchange.getRequestMethod().equals("PUT") ? ",\"secret\":\"" + "a".repeat(64) + "\"" : "";
                respond(exchange, "application/json", "{\"webhook\":{\"id\":\"wh-1\",\"url\":\"https://erp.example/epostak\",\"events\":[\"document.received\"],\"active\":true,\"failedAttempts\":0,\"createdAt\":\"2026-07-15T10:00:00Z\",\"updatedAt\":\"2026-07-15T10:00:00Z\"}" + secret + "}");
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

    /** Closes a real HTTP exchange without response headers to force an IOException. */
    private static final class TransportDropServer implements AutoCloseable {
        private final HttpServer server;
        private final boolean recoverAfterFirstDrop;
        private final List<CapturedRequest> requests = new ArrayList<>();

        private TransportDropServer(HttpServer server, boolean recoverAfterFirstDrop) {
            this.server = server;
            this.recoverAfterFirstDrop = recoverAfterFirstDrop;
        }

        static TransportDropServer start(boolean recoverAfterFirstDrop) throws IOException {
            HttpServer httpServer = HttpServer.create(
                    new InetSocketAddress(InetAddress.getLoopbackAddress(), 0),
                    0
            );
            TransportDropServer transportServer = new TransportDropServer(httpServer, recoverAfterFirstDrop);
            httpServer.createContext("/", transportServer::handle);
            httpServer.start();
            return transportServer;
        }

        String baseUrl() {
            return "http://" + server.getAddress().getHostString() + ":" + server.getAddress().getPort() + "/api/v1";
        }

        List<CapturedRequest> requests() {
            return requests;
        }

        private void handle(HttpExchange exchange) throws IOException {
            String path = exchange.getRequestURI().getPath();
            if (path.equals("/sapi/v1/auth/token")) {
                respond(exchange, 200, "{\"access_token\":\"token\",\"refresh_token\":\"refresh\",\"expires_in\":3600}");
                return;
            }

            String body = new String(exchange.getRequestBody().readAllBytes(), StandardCharsets.UTF_8);
            Headers headers = exchange.getRequestHeaders();
            requests.add(new CapturedRequest(
                    exchange.getRequestMethod(),
                    path,
                    exchange.getRequestURI().getRawQuery(),
                    headers.getFirst("X-Firm-Id"),
                    headers.getFirst("X-Peppol-Participant-Id"),
                    headers.getFirst("Idempotency-Key"),
                    body
            ));

            if (!recoverAfterFirstDrop || requests.size() == 1) {
                exchange.close();
                return;
            }

            respond(exchange, 201, "{\"id\":\"doc-transport\",\"state\":\"queued\"}");
        }

        private static void respond(HttpExchange exchange, int status, String body) throws IOException {
            byte[] bytes = body.getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            exchange.sendResponseHeaders(status, bytes.length);
            exchange.getResponseBody().write(bytes);
            exchange.close();
        }

        @Override
        public void close() {
            server.stop(0);
        }
    }

    private record ErrorSpec(int status, String body, Map<String, String> headers) {}

    private static final class ErrorServer implements AutoCloseable {
        private final HttpServer server;
        private final java.util.Queue<ErrorSpec> responses;
        private final List<CapturedRequest> requests = new ArrayList<>();

        private ErrorServer(HttpServer server, List<ErrorSpec> responses) {
            this.server = server;
            this.responses = new java.util.ArrayDeque<>(responses);
        }

        static ErrorServer start(int status, String body, Map<String, String> headers) throws IOException {
            return startSequence(List.of(new ErrorSpec(status, body, headers)));
        }

        static ErrorServer startSequence(List<ErrorSpec> responses) throws IOException {
            HttpServer httpServer = HttpServer.create(new InetSocketAddress(InetAddress.getLoopbackAddress(), 0), 0);
            ErrorServer errorServer = new ErrorServer(httpServer, responses);
            httpServer.createContext("/", errorServer::handle);
            httpServer.start();
            return errorServer;
        }

        String baseUrl() {
            return "http://" + server.getAddress().getHostString() + ":" + server.getAddress().getPort() + "/api/v1";
        }

        List<CapturedRequest> requests() { return requests; }

        private void handle(HttpExchange exchange) throws IOException {
            if (exchange.getRequestURI().getPath().equals("/sapi/v1/auth/token")) {
                respond(exchange, 200, "{\"access_token\":\"token\",\"refresh_token\":\"refresh\",\"expires_in\":3600}", Map.of());
                return;
            }
            String requestBody = new String(exchange.getRequestBody().readAllBytes(), StandardCharsets.UTF_8);
            Headers requestHeaders = exchange.getRequestHeaders();
            requests.add(new CapturedRequest(
                    exchange.getRequestMethod(),
                    exchange.getRequestURI().getPath(),
                    exchange.getRequestURI().getRawQuery(),
                    requestHeaders.getFirst("X-Firm-Id"),
                    requestHeaders.getFirst("X-Peppol-Participant-Id"),
                    requestHeaders.getFirst("Idempotency-Key"),
                    requestBody
            ));
            ErrorSpec response = responses.remove();
            respond(exchange, response.status(), response.body(), response.headers());
        }

        private static void respond(
                HttpExchange exchange,
                int status,
                String body,
                Map<String, String> headers
        ) throws IOException {
            byte[] bytes = body.getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            headers.forEach((name, value) -> exchange.getResponseHeaders().add(name, value));
            exchange.sendResponseHeaders(status, bytes.length);
            exchange.getResponseBody().write(bytes);
            exchange.close();
        }

        @Override
        public void close() {
            server.stop(0);
        }
    }
}
