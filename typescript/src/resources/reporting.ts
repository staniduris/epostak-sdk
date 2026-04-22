import { BaseResource, buildQuery } from "../utils/request.js";
import type { StatisticsParams, Statistics } from "../types.js";

/**
 * Resource for retrieving document statistics and reports.
 *
 * @example
 * ```typescript
 * const stats = await client.reporting.statistics({ from: '2026-01-01', to: '2026-03-31' });
 * console.log(`Sent: ${stats.outbound.total}, Received: ${stats.inbound.total}`);
 * ```
 */
export class ReportingResource extends BaseResource {
  /**
   * Get aggregated document statistics for a time period.
   * Returns counts of sent/received documents, delivery success rates,
   * and acknowledgment status.
   *
   * @param params - Optional date range (defaults to last 30 days)
   * @returns Aggregated statistics for outbound and inbound documents
   *
   * @example
   * ```typescript
   * // Get stats for Q1 2026
   * const stats = await client.reporting.statistics({
   *   from: '2026-01-01',
   *   to: '2026-03-31',
   * });
   * console.log(`Delivery rate: ${stats.outbound.delivered}/${stats.outbound.total}`);
   * console.log(`Pending inbox: ${stats.inbound.pending}`);
   * ```
   */
  statistics(params?: StatisticsParams): Promise<Statistics> {
    return this.request(
      "GET",
      `/reporting/statistics${buildQuery({
        period: params?.period,
        from: params?.from,
        to: params?.to,
      })}`,
    );
  }
}
