import { BaseResource } from "../utils/request.js";
import type { Account } from "../types.js";

export class AccountResource extends BaseResource {
  get(): Promise<Account> {
    return this.request("GET", "/account");
  }
}
