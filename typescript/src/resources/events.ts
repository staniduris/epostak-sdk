import { BaseResource, buildQuery } from "../utils/request.js";
import type {
  WebhookQueueItem,
  WebhookQueueParams,
  WebhookQueueResponse,
} from "../types.js";

type EventsPullWireResponse = WebhookQueueResponse & {
  events?: WebhookQueueItem[];
};

function normalizeEventsPullResponse(
  response: EventsPullWireResponse,
): WebhookQueueResponse {
  return {
    ...response,
    items: response.items ?? response.events ?? [],
  };
}

/**
 * Pull/ack event facade over the webhook queue.
 */
export class EventsResource extends BaseResource {
  async pull(params?: WebhookQueueParams): Promise<WebhookQueueResponse> {
    const response = await this.request<EventsPullWireResponse>(
      "GET",
      `/events/pull${buildQuery({
        limit: params?.limit,
        event_type: params?.event_type,
      })}`,
    );
    return normalizeEventsPullResponse(response);
  }

  ack(eventId: string): Promise<{ acknowledged: boolean }> {
    return this.request(
      "POST",
      `/events/${encodeURIComponent(eventId)}/ack`,
    );
  }

  batchAck(eventIds: string[]): Promise<{ acknowledged: number }> {
    return this.request("POST", "/events/batch-ack", {
      event_ids: eventIds,
    });
  }
}
