import { BaseResource, buildQuery } from "../utils/request.js";
import type { ClientConfig } from "../utils/request.js";
import type {
  CreateWebhookRequest,
  UpdateWebhookRequest,
  Webhook,
  WebhookDetail,
  WebhookWithDeliveries,
  WebhookListResponse,
  WebhookQueueParams,
  WebhookQueueResponse,
  WebhookQueueAllParams,
  WebhookQueueAllResponse,
  WebhookTestResponse,
  WebhookDeliveriesParams,
  WebhookDeliveriesResponse,
  WebhookEvent,
} from "../types.js";

/**
 * Sub-resource for the webhook pull queue — an alternative to push webhooks.
 * Use the pull queue when your server cannot receive inbound HTTPS requests.
 * Events accumulate in the queue and must be acknowledged after processing.
 *
 * @example
 * ```typescript
 * // Poll-based event consumption loop
 * const { items, has_more } = await client.webhooks.queue.pull({ limit: 50 });
 * for (const item of items) {
 *   await processEvent(item);
 *   await client.webhooks.queue.ack(item.id);
 * }
 * ```
 */
export class WebhookQueueResource extends BaseResource {
  /**
   * Pull unacknowledged events from the webhook queue.
   * Events remain in the queue until explicitly acknowledged via `ack()` or `batchAck()`.
   *
   * @param params - Optional limit and event type filter
   * @returns Array of queue items and whether more items are available
   *
   * @example
   * ```typescript
   * const { items, has_more } = await client.webhooks.queue.pull({
   *   limit: 20,
   *   event_type: 'document.received',
   * });
   * ```
   */
  pull(params?: WebhookQueueParams): Promise<WebhookQueueResponse> {
    return this.request(
      "GET",
      `/webhook-queue${buildQuery({
        limit: params?.limit,
        event_type: params?.event_type,
      })}`,
    );
  }

  /**
   * Acknowledge (remove) a single event from the queue after processing.
   *
   * @param eventId - The event ID to acknowledge
   * @returns void
   *
   * @example
   * ```typescript
   * await client.webhooks.queue.ack('event-uuid');
   * ```
   */
  ack(eventId: string): Promise<void> {
    return this.request(
      "DELETE",
      `/webhook-queue/${encodeURIComponent(eventId)}`,
    );
  }

  /**
   * Acknowledge (remove) multiple events from the queue in a single request.
   *
   * @param eventIds - Array of event IDs to acknowledge
   * @returns void
   *
   * @example
   * ```typescript
   * const { items } = await client.webhooks.queue.pull({ limit: 50 });
   * // Process all items...
   * await client.webhooks.queue.batchAck(items.map(i => i.id));
   * ```
   */
  batchAck(eventIds: string[]): Promise<void> {
    return this.request("POST", "/webhook-queue/batch-ack", {
      event_ids: eventIds,
    });
  }

  /**
   * Pull events across all managed firms (integrator endpoint).
   * Only available with integrator API keys (`sk_int_*`).
   * Use the `since` parameter for cursor-based polling.
   *
   * @param params - Optional limit and since timestamp for cursor-based polling
   * @returns Array of events across all firms with count
   *
   * @example
   * ```typescript
   * const { events, count } = await client.webhooks.queue.pullAll({
   *   since: '2026-04-11T00:00:00Z',
   *   limit: 200,
   * });
   * ```
   */
  pullAll(params?: WebhookQueueAllParams): Promise<WebhookQueueAllResponse> {
    return this.request(
      "GET",
      `/webhook-queue/all${buildQuery({
        limit: params?.limit,
        since: params?.since,
      })}`,
    );
  }

  /**
   * Acknowledge (remove) multiple events from the cross-firm queue (integrator endpoint).
   * Only available with integrator API keys (`sk_int_*`).
   *
   * @param eventIds - Array of event IDs to acknowledge
   * @returns Object with the count of acknowledged events
   *
   * @example
   * ```typescript
   * const { events } = await client.webhooks.queue.pullAll({ limit: 100 });
   * // Process all events...
   * const { acknowledged } = await client.webhooks.queue.batchAckAll(
   *   events.map(e => e.event_id),
   * );
   * ```
   */
  batchAckAll(eventIds: string[]): Promise<{ acknowledged: number }> {
    return this.request("POST", "/webhook-queue/all/batch-ack", {
      event_ids: eventIds,
    });
  }
}

/**
 * Resource for managing webhook subscriptions and the pull queue.
 * Webhooks notify your server about document events (sent, received, validated).
 * Choose between push webhooks (server receives HTTPS POST) or the pull queue
 * (your code polls for events).
 *
 * @example
 * ```typescript
 * // Create a push webhook
 * const webhook = await client.webhooks.create({
 *   url: 'https://example.com/webhooks/epostak',
 *   events: ['document.received', 'document.sent'],
 * });
 * // Store webhook.secret for HMAC verification
 * ```
 */
export class WebhooksResource extends BaseResource {
  /** Sub-resource for the pull queue (polling-based event consumption) */
  queue: WebhookQueueResource;

  constructor(config: ClientConfig) {
    super(config);
    this.queue = new WebhookQueueResource(config);
  }

  /**
   * Create a new webhook subscription. Returns the HMAC-SHA256 signing secret
   * which is only available at creation time — store it securely.
   *
   * @param body - Webhook URL and optional event filter
   * @returns Webhook details including the one-time signing secret
   *
   * @example
   * ```typescript
   * const webhook = await client.webhooks.create({
   *   url: 'https://example.com/webhooks',
   *   events: ['document.received'],
   * });
   * console.log(webhook.secret); // Store this securely!
   * ```
   */
  create(body: CreateWebhookRequest): Promise<WebhookDetail> {
    return this.request("POST", "/webhooks", body);
  }

  /**
   * List all webhook subscriptions for the current account.
   *
   * @returns Array of webhook subscriptions
   *
   * @example
   * ```typescript
   * const webhooks = await client.webhooks.list();
   * webhooks.forEach(w => console.log(w.url, w.isActive));
   * ```
   */
  async list(): Promise<Webhook[]> {
    const res = await this.request<WebhookListResponse>("GET", "/webhooks");
    return res.data;
  }

  /**
   * Get a webhook subscription by ID, including recent delivery history.
   * Use the delivery history to debug failed webhook deliveries.
   *
   * @param id - Webhook UUID
   * @returns Webhook details with delivery history
   *
   * @example
   * ```typescript
   * const webhook = await client.webhooks.get('webhook-uuid');
   * const failedDeliveries = webhook.deliveries.filter(d => d.status === 'failed');
   * ```
   */
  get(id: string): Promise<WebhookWithDeliveries> {
    return this.request("GET", `/webhooks/${encodeURIComponent(id)}`);
  }

  /**
   * Update a webhook subscription. Use this to change the URL, event filter,
   * or pause/resume the webhook.
   *
   * @param id - Webhook UUID
   * @param body - Fields to update (omit to leave unchanged)
   * @returns The updated webhook
   *
   * @example
   * ```typescript
   * // Pause a webhook
   * await client.webhooks.update('webhook-uuid', { isActive: false });
   *
   * // Change URL and events
   * await client.webhooks.update('webhook-uuid', {
   *   url: 'https://new-url.com/webhooks',
   *   events: ['document.received', 'document.validated'],
   * });
   * ```
   */
  update(id: string, body: UpdateWebhookRequest): Promise<Webhook> {
    return this.request("PATCH", `/webhooks/${encodeURIComponent(id)}`, body);
  }

  /**
   * Delete a webhook subscription. Stops all future deliveries for this webhook.
   *
   * @param id - Webhook UUID to delete
   * @returns Confirmation with `deleted: true`
   *
   * @example
   * ```typescript
   * await client.webhooks.delete('webhook-uuid');
   * ```
   */
  delete(id: string): Promise<{ deleted: boolean }> {
    return this.request("DELETE", `/webhooks/${encodeURIComponent(id)}`);
  }

  /**
   * Send a test event to a webhook endpoint. Useful for verifying your
   * webhook URL is reachable and responding correctly.
   *
   * @param id - Webhook UUID to test
   * @param event - Event type to simulate (defaults to server-chosen event)
   * @returns Test result with success status, HTTP status code, and response time
   *
   * @example
   * ```typescript
   * const result = await client.webhooks.test('webhook-uuid');
   * console.log(result.success, result.responseTime + 'ms');
   * ```
   */
  test(id: string, event?: WebhookEvent): Promise<WebhookTestResponse> {
    const body: Record<string, string> = {};
    if (event) body.event = event;
    return this.request(
      "POST",
      `/webhooks/${encodeURIComponent(id)}/test`,
      body,
    );
  }

  /**
   * Get paginated delivery history for a webhook. Use this to inspect
   * individual delivery attempts, filter by status, and debug failures.
   *
   * @param id - Webhook UUID
   * @param params - Optional pagination and filter parameters
   * @returns Paginated list of delivery records with total count
   *
   * @example
   * ```typescript
   * const { deliveries, total } = await client.webhooks.deliveries('webhook-uuid', {
   *   status: 'FAILED',
   *   limit: 50,
   * });
   * ```
   */
  deliveries(
    id: string,
    params?: WebhookDeliveriesParams,
  ): Promise<WebhookDeliveriesResponse> {
    return this.request(
      "GET",
      `/webhooks/${encodeURIComponent(id)}/deliveries${buildQuery({
        limit: params?.limit,
        offset: params?.offset,
        status: params?.status,
        event: params?.event,
      })}`,
    );
  }
}
