import { BaseResource, buildQuery } from "../utils/request.js";
import type {
  FirmSummary,
  FirmDetail,
  FirmsListResponse,
  FirmDocumentsParams,
  InboxListResponse,
  PeppolIdentifierResponse,
  AssignFirmRequest,
  AssignFirmResponse,
  BatchAssignFirmsRequest,
  BatchAssignFirmsResponse,
} from "../types.js";

export class FirmsResource extends BaseResource {
  async list(): Promise<FirmSummary[]> {
    const res = await this.request<FirmsListResponse>("GET", "/firms");
    return res.firms;
  }

  get(id: string): Promise<FirmDetail> {
    return this.request("GET", `/firms/${encodeURIComponent(id)}`);
  }

  documents(
    id: string,
    params?: FirmDocumentsParams,
  ): Promise<InboxListResponse> {
    return this.request(
      "GET",
      `/firms/${encodeURIComponent(id)}/documents${buildQuery({
        offset: params?.offset,
        limit: params?.limit,
        direction: params?.direction,
      })}`,
    );
  }

  registerPeppolId(
    id: string,
    peppolId: { scheme: string; identifier: string },
  ): Promise<PeppolIdentifierResponse> {
    return this.request(
      "POST",
      `/firms/${encodeURIComponent(id)}/peppol-identifiers`,
      peppolId,
    );
  }

  assign(body: AssignFirmRequest): Promise<AssignFirmResponse> {
    return this.request("POST", "/firms/assign", body);
  }

  assignBatch(
    body: BatchAssignFirmsRequest,
  ): Promise<BatchAssignFirmsResponse> {
    return this.request("POST", "/firms/assign/batch", body);
  }
}
