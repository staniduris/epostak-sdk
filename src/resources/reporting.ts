import { BaseResource, buildQuery } from "../utils/request.js";
import type { StatisticsParams, Statistics } from "../types.js";

export class ReportingResource extends BaseResource {
  statistics(params?: StatisticsParams): Promise<Statistics> {
    return this.request("GET", `/reporting/statistics${buildQuery({
      from: params?.from,
      to: params?.to,
    })}`);
  }
}
