import { BaseResource, buildQuery, request } from "../utils/request.js";
import type { RequestOptions } from "../utils/request.js";
import type { RateLimitState } from "../types.js";
import type { SapiAcknowledgeResponse, SapiDocumentDetail, SapiDocumentListParams, SapiDocumentListResponse, SapiSendDocumentRequest, SapiSendDocumentResponse } from "../types.js";

export interface SapiParticipantOptions {
  participantId: string;
}

export interface SapiSendOptions extends SapiParticipantOptions {
  idempotencyKey: string;
}

export class SapiResource extends BaseResource {
  private get sapiBaseUrl(): string {
    return this.config.baseUrl.replace(/\/api\/v1\/?$/, "");
  }

  private options(
    participantId: string,
    extra?: RequestOptions,
  ): RequestOptions & { maxRetries: number; onRateLimit?: (s: RateLimitState) => void } {
    const opts: RequestOptions & { maxRetries: number; onRateLimit?: (s: RateLimitState) => void } = {
      ...extra,
      headers: {
        ...extra?.headers,
        "X-Peppol-Participant-Id": participantId,
      },
      maxRetries: this.config.maxRetries,
    };
    if (this.config.onRateLimit) opts.onRateLimit = this.config.onRateLimit;
    return opts;
  }

  send(
    body: SapiSendDocumentRequest,
    options: SapiSendOptions,
  ): Promise<SapiSendDocumentResponse> {
    return request<SapiSendDocumentResponse>(
      this.sapiBaseUrl,
      this.config.tokenManager,
      this.config.firmId,
      "POST",
      "/sapi/v1/document/send",
      body,
      this.options(options.participantId, { idempotencyKey: options.idempotencyKey }),
    );
  }

  receive(
    params: SapiDocumentListParams,
    options: SapiParticipantOptions,
  ): Promise<SapiDocumentListResponse> {
    return request<SapiDocumentListResponse>(
      this.sapiBaseUrl,
      this.config.tokenManager,
      this.config.firmId,
      "GET",
      `/sapi/v1/document/receive${buildQuery({
        limit: params.limit,
        status: params.status,
        pageToken: params.pageToken,
      })}`,
      undefined,
      this.options(options.participantId),
    );
  }

  get(documentId: string, options: SapiParticipantOptions): Promise<SapiDocumentDetail> {
    return request<SapiDocumentDetail>(
      this.sapiBaseUrl,
      this.config.tokenManager,
      this.config.firmId,
      "GET",
      `/sapi/v1/document/receive/${encodeURIComponent(documentId)}`,
      undefined,
      this.options(options.participantId),
    );
  }

  acknowledge(
    documentId: string,
    options: SapiParticipantOptions,
  ): Promise<SapiAcknowledgeResponse> {
    return request<SapiAcknowledgeResponse>(
      this.sapiBaseUrl,
      this.config.tokenManager,
      this.config.firmId,
      "POST",
      `/sapi/v1/document/receive/${encodeURIComponent(documentId)}/acknowledge`,
      undefined,
      this.options(options.participantId),
    );
  }
}
