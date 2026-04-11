import { BaseResource, buildQuery } from "../utils/request.js";
import type { ClientConfig } from "../utils/request.js";
import type {
  SendDocumentRequest,
  SendDocumentResponse,
  UpdateDocumentRequest,
  InboxDocument,
  InboxListParams,
  InboxListResponse,
  InboxDocumentDetailResponse,
  AcknowledgeResponse,
  InboxAllParams,
  InboxAllResponse,
  DocumentStatusResponse,
  DocumentEvidenceResponse,
  InvoiceRespondRequest,
  InvoiceRespondResponse,
  ValidationResult,
  PreflightRequest,
  PreflightResult,
  ConvertRequest,
  ConvertResult,
} from "../types.js";

export class InboxResource extends BaseResource {
  list(params?: InboxListParams): Promise<InboxListResponse> {
    return this.request("GET", `/documents/inbox${buildQuery({
      offset: params?.offset,
      limit: params?.limit,
      status: params?.status,
      since: params?.since,
    })}`);
  }

  get(id: string): Promise<InboxDocumentDetailResponse> {
    return this.request("GET", `/documents/inbox/${encodeURIComponent(id)}`);
  }

  acknowledge(id: string): Promise<AcknowledgeResponse> {
    return this.request("POST", `/documents/inbox/${encodeURIComponent(id)}/acknowledge`);
  }

  listAll(params?: InboxAllParams): Promise<InboxAllResponse> {
    return this.request("GET", `/documents/inbox/all${buildQuery({
      offset: params?.offset,
      limit: params?.limit,
      status: params?.status,
      since: params?.since,
      firm_id: params?.firm_id,
    })}`);
  }
}

export class DocumentsResource extends BaseResource {
  /** Access received documents */
  inbox: InboxResource;

  constructor(config: ClientConfig) {
    super(config);
    this.inbox = new InboxResource(config);
  }

  get(id: string): Promise<InboxDocument> {
    return this.request("GET", `/documents/${encodeURIComponent(id)}`);
  }

  update(id: string, body: UpdateDocumentRequest): Promise<InboxDocument> {
    return this.request("PATCH", `/documents/${encodeURIComponent(id)}`, body);
  }

  send(body: SendDocumentRequest): Promise<SendDocumentResponse> {
    return this.request("POST", "/documents/send", body);
  }

  status(id: string): Promise<DocumentStatusResponse> {
    return this.request("GET", `/documents/${encodeURIComponent(id)}/status`);
  }

  evidence(id: string): Promise<DocumentEvidenceResponse> {
    return this.request("GET", `/documents/${encodeURIComponent(id)}/evidence`);
  }

  async pdf(id: string): Promise<Buffer> {
    const res = await this.request<Response>("GET", `/documents/${encodeURIComponent(id)}/pdf`, undefined, { rawResponse: true });
    return Buffer.from(await res.arrayBuffer());
  }

  async ubl(id: string): Promise<string> {
    const res = await this.request<Response>("GET", `/documents/${encodeURIComponent(id)}/ubl`, undefined, { rawResponse: true });
    return res.text();
  }

  respond(id: string, body: InvoiceRespondRequest): Promise<InvoiceRespondResponse> {
    return this.request("POST", `/documents/${encodeURIComponent(id)}/respond`, body);
  }

  validate(body: SendDocumentRequest): Promise<ValidationResult> {
    return this.request("POST", "/documents/validate", body);
  }

  preflight(body: PreflightRequest): Promise<PreflightResult> {
    return this.request("POST", "/documents/preflight", body);
  }

  convert(body: ConvertRequest): Promise<ConvertResult> {
    return this.request("POST", "/documents/convert", body);
  }
}
