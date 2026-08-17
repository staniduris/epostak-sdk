import { BaseResource, buildQuery } from "../utils/request.js";
import type {
  WhiteLabelListParticipantsParams,
  WhiteLabelMigrationCodeResponse,
  WhiteLabelParticipant,
  WhiteLabelParticipantList,
  WhiteLabelParticipantMigrationRequest,
  WhiteLabelParticipantOperation,
  WhiteLabelParticipantRegistrationRequest,
} from "../types.js";

function whiteLabelIdempotencyKey(value: string): string {
  if (
    typeof value !== "string" ||
    value.trim().length === 0 ||
    new TextEncoder().encode(value).length > 255
  ) {
    throw new Error("White Label idempotency key must be 1-255 UTF-8 bytes");
  }
  return value;
}

/**
 * Integrator-scoped participant lifecycle for approved White Label providers.
 *
 * This resource never sends `X-Firm-Id`. The API derives ownership from the
 * authenticated White Label integrator and only exposes participants managed
 * by that integrator.
 */
export class WhiteLabelResource extends BaseResource {
  listParticipants(
    params?: WhiteLabelListParticipantsParams,
  ): Promise<WhiteLabelParticipantList> {
    return this.request(
      "GET",
      `/white-label/participants${buildQuery({
        limit: params?.limit,
        cursor: params?.cursor,
      })}`,
      undefined,
      { omitFirmId: true },
    );
  }

  registerParticipant(
    body: WhiteLabelParticipantRegistrationRequest,
    idempotencyKey: string,
  ): Promise<WhiteLabelParticipantOperation> {
    const key = whiteLabelIdempotencyKey(idempotencyKey);
    return this.request(
      "POST",
      "/white-label/participants/registrations",
      body,
      { omitFirmId: true, idempotencyKey: key, retry: true },
    );
  }

  migrateParticipant(
    body: WhiteLabelParticipantMigrationRequest,
    idempotencyKey: string,
  ): Promise<WhiteLabelParticipantOperation> {
    const key = whiteLabelIdempotencyKey(idempotencyKey);
    return this.request(
      "POST",
      "/white-label/participants/migrations",
      body,
      { omitFirmId: true, idempotencyKey: key, retry: true },
    );
  }

  getParticipant(participantId: string): Promise<WhiteLabelParticipant> {
    return this.request(
      "GET",
      `/white-label/participants/${encodeURIComponent(participantId)}`,
      undefined,
      { omitFirmId: true },
    );
  }

  requestMigrationCode(
    participantId: string,
    idempotencyKey: string,
  ): Promise<WhiteLabelMigrationCodeResponse> {
    const key = whiteLabelIdempotencyKey(idempotencyKey);
    return this.request(
      "POST",
      `/white-label/participants/${encodeURIComponent(participantId)}/migration-code`,
      undefined,
      { omitFirmId: true, idempotencyKey: key, retry: true },
    );
  }

  getOperation(operationId: string): Promise<WhiteLabelParticipantOperation> {
    return this.request(
      "GET",
      `/white-label/operations/${encodeURIComponent(operationId)}`,
      undefined,
      { omitFirmId: true },
    );
  }
}
