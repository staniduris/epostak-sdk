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
} from "../types.js";

export class WebhookQueueResource extends BaseResource {
  pull(params?: WebhookQueueParams): Promise<WebhookQueueResponse> {
    return this.request("GET", `/webhook-queue${buildQuery({
      limit: params?.limit,
      event_type: params?.event_type,
    })}`);
  }

  ack(eventId: string): Promise<void> {
    return this.request("DELETE", `/webhook-queue/${encodeURIComponent(eventId)}`);
  }

  batchAck(eventIds: string[]): Promise<void> {
    return this.request("POST", "/webhook-queue/batch-ack", { event_ids: eventIds });
  }

  pullAll(params?: WebhookQueueAllParams): Promise<WebhookQueueAllResponse> {
    return this.request("GET", `/webhook-queue/all${buildQuery({
      limit: params?.limit,
      since: params?.since,
    })}`);
  }

  batchAckAll(eventIds: string[]): Promise<{ acknowledged: number }> {
    return this.request("POST", "/webhook-queue/all/batch-ack", { event_ids: eventIds });
  }
}

export class WebhooksResource extends BaseResource {
  /** Access the webhook pull queue for polling-based consumption */
  queue: WebhookQueueResource;

  constructor(config: ClientConfig) {
    super(config);
    this.queue = new WebhookQueueResource(config);
  }

  create(body: CreateWebhookRequest): Promise<WebhookDetail> {
    return this.request("POST", "/webhooks", body);
  }

  async list(): Promise<Webhook[]> {
    const res = await this.request<WebhookListResponse>("GET", "/webhooks");
    return res.data;
  }

  get(id: string): Promise<WebhookWithDeliveries> {
    return this.request("GET", `/webhooks/${encodeURIComponent(id)}`);
  }

  update(id: string, body: UpdateWebhookRequest): Promise<Webhook> {
    return this.request("PATCH", `/webhooks/${encodeURIComponent(id)}`, body);
  }

  delete(id: string): Promise<{ deleted: boolean }> {
    return this.request("DELETE", `/webhooks/${encodeURIComponent(id)}`);
  }
}
