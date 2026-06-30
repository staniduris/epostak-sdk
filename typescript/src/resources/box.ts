import { BaseResource, buildQuery } from "../utils/request.js";
import type {
  BoxItem,
  BoxItemDetail,
  BoxCreateRequest,
  BoxListParams,
  BoxListResponse,
  BoxScheduleRequest,
} from "../types.js";

/**
 * ePošťák Box durable execution layer for staged, scheduled, and retryable
 * Peppol dispatch.
 */
export class BoxResource extends BaseResource {
  list(params?: BoxListParams): Promise<BoxListResponse> {
    return this.request(
      "GET",
      `/box/items${buildQuery({
        status: params?.status,
        direction: params?.direction,
        limit: params?.limit,
        offset: params?.offset,
      })}`,
    );
  }

  create(body: BoxCreateRequest): Promise<BoxItem & Record<string, unknown>> {
    return this.request("POST", "/box/items", body);
  }

  get(itemId: string): Promise<BoxItemDetail> {
    return this.request("GET", `/box/items/${encodeURIComponent(itemId)}`);
  }

  schedule(itemId: string, body: BoxScheduleRequest): Promise<BoxItem> {
    return this.request(
      "POST",
      `/box/items/${encodeURIComponent(itemId)}/schedule`,
      body,
    );
  }

  sendNow(itemId: string): Promise<Record<string, unknown>> {
    return this.request(
      "POST",
      `/box/items/${encodeURIComponent(itemId)}/send-now`,
    );
  }

  retry(itemId: string): Promise<BoxItem> {
    return this.request("POST", `/box/items/${encodeURIComponent(itemId)}/retry`);
  }

  cancel(itemId: string): Promise<BoxItem> {
    return this.request("POST", `/box/items/${encodeURIComponent(itemId)}/cancel`);
  }
}
