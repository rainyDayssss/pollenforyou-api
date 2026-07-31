# AGENT.md

Guidance for AI agents and developers working in the **Pollen For You Online Kiosk** backend.

> **Source of truth:** [`specs.md`](specs.md) — Software Requirements Specification (SRS) **v6.0** (Unified Single-Ledger State Machine Architecture). This file is a distillation; when in doubt, follow the SRS.

---

## 1. Project Overview

Pollen For You is a flower + coffee ordering kiosk. Customers assemble orders via a public web app, submit a checkout form (delivery details + raw Facebook Messenger username), and receive a structured Order Number. Admins monitor a shared live queue, claim orders under a 15-minute workspace lock, verify payments manually in standalone Messenger, and promote orders through a fulfillment state machine.

The system is deliberately **workerless**: transient unverified checkouts are never deleted or swept by background jobs. They expire via **lazy state mutation** (`Status = 'Expired'`) triggered on-demand inside order writes.

**Unified single ledger:** Transient checkouts and confirmed sales live in **one** `Orders` table, driven by the order state machine — there is no separate draft/cart table. This keeps infrastructure minimal on Azure SQL Free Tier while remaining a clean data source for future forecasting.

**Post-MVP (do not build now):** Automated Meta Webhook ingestion and Hugging Face demand forecasting are explicitly deferred. Design must not preclude layering them on later.

## 2. Tech Stack

| Layer | Technology |
| :--- | :--- |
| Language / Framework | C# / ASP.NET Core Web API (**.NET 10**) |
| Architecture | Layered N-Tier: Controllers → FluentValidation → Services → AutoMapper → Repositories → EF Core (DTOs + Entities) |
| ORM / Database | EF Core 10 with **Fluent API** only (no Data Annotations); Azure SQL Database / SQL Server Free Tier |
| Validation | FluentValidation pipeline (validators: `CheckoutRequestDtoValidator`, `OrderConfirmationValidator`, etc.) |
| Mapping | AutoMapper Profiles (`OrderMappingProfile`, `ProductMappingProfile`); queries use `ProjectTo<T>()` |
| Identity / Auth | ASP.NET Core Identity Core + JWT Bearer + SHA-256 hashed refresh tokens |
| CI/CD | GitHub Actions (build → lint → unit tests → deploy to Azure App Service) |
| Client (context only) | React 19 / TypeScript / Vite, TanStack Query v5 (5s queue polling) |

## 3. Build, Run & Verify

```bash
dotnet restore          # restore packages
dotnet build            # compile
dotnet run              # run the Web API locally
dotnet test             # run unit tests
```

- Target framework is `net10.0`; nullable reference types and implicit usings are enabled.
- `Program.cs` currently contains only the default scaffold — the SRS layers and packages are the target to implement.

## 4. Architecture & Conventions (non-negotiable)

Follow these patterns exactly. They are not suggestions.

1. **Layered N-Tier:** Controllers (presentation) → FluentValidation (input boundary) → Services (domain logic) → Repositories (data access) → EF Core. DTOs are data contracts; entities are domain models. No logic in controllers; no `DbContext` usage outside repositories.
2. **FluentValidation, not Data Annotations:** Every inbound HTTP request DTO is validated by a dedicated validator before it reaches a service. Failures return standard `400 Bad Request` validation problem details.
3. **AutoMapper Profiles for all projection:** Register dedicated profiles. Use `.ProjectTo<T>()` on IQueryables for SQL-efficient projection.
4. **EF Core Fluent API only:** No Data Annotations. Use `IEntityTypeConfiguration<T>` classes wired in `DbContext.OnModelCreating()`. Configurations must explicitly set: table names, primary keys, required string lengths, FK constraints, `RowVersion` concurrency tokens (`builder.Property(o => o.RowVersion).IsRowVersion()`), and global soft-delete query filters (`HasQueryFilter(e => e.IsActive)`).
5. **Reads are `AsNoTracking()`:** All queue and catalog queries use `AsNoTracking()`, combine logical-expiry/soft-delete filters, and paginate at the database level (`.Skip((page-1)*pageSize).Take(pageSize)` → SQL `OFFSET...FETCH NEXT`).
6. **Deterministic Order Numbers:** `PFY-YYYYMMDD-XXXX` (e.g., `PFY-20260731-0001`) via a **daily counter** (`COUNT` of today's orders + 1) guarded by the unique `OrderNumber` index with a retry loop on 2601/2627 — workerless, no background reset job. Never random reference codes.
7. **Immutable record DTOs:** Every DTO is a `record` with `{ get; init; }` properties only — never `{ get; set; }`. DTOs are data contracts, not mutable holders. When a service/repository must enrich a DTO after mapping (e.g., populating `Roles` — Identity exposes no `Roles` navigation on the user, so it's filled post-`ProjectTo`/`Map`), rebuild it with a `with`-expression: `dto with { Roles = roles }`, or return a fresh list of rebuilt items (`UserRepository.PopulateRolesAsync`). System.Text.Json binds init-only request DTOs fine.

## 5. Core Domain — Order State Machine

```
Pending ───► In Production ───► Ready for Dispatch ───► Dispatched ───► Completed
   │
   ├───► Expired   (automatic lazy mutation after 2 hours)
   └───► Cancelled (manual admin rejection)
```

Key rules:

- **Checkout (public):** Server recalculates totals from DB base prices — **client-submitted pricing is discarded**. Order created as `Pending` with `ExpiresAt = UtcNow.AddHours(2)`. Cart calculations happen entirely in client memory.
- **Checkout field contract (`CheckoutRequestDto`):** Customer Name, Facebook Messenger Username, Receiver Name, Receiver Contact Number, Delivery Address, Delivery Date, Booking Time, optional Card Message. Validated by `CheckoutRequestDtoValidator`. Messenger username case is preserved verbatim.
- **Settlement (admin):** Inside one **atomic transaction**: set `Status = 'In Production'` + `SettledByAdminId` + clear claims → write order items with **frozen snapshots** (product name + purchase price) → write payment entity (stage: `Downpayment`/`Full Payment`, method, amount, reference code) → run hitchhiker lazy eviction.
- **Immutable ledger:** Verified order entries (settled/fulfilled) are never mutated or deleted.
- **Fulfillment:** Status transitions (`Ready for Dispatch`, `Dispatched`, `Completed`, `Cancelled`) go through `PATCH /api/admin/orders/{id}/status`.

## 6. Lazy Eviction Engine (workerless)

- **Never** run background workers/hangfire/hosted services for eviction — protects Azure Free Tier CPU/connection thresholds.
- Eviction = bulk UPDATE: `SET Status = 'Expired' WHERE Status = 'Pending' AND ExpiresAt <= UtcNow`.
- Two trigger points only:
  1. `OrderRepository.ExecuteLazyEvictionAsync()` at **checkout submission** (before creating the new order).
  2. **Hitchhiker eviction** inside admin settlement transactions (sweeps unrelated expired pending records).
- During off-hours, expired rows stay in the table but are logically isolated on reads via `WHERE Status = 'Pending' AND ExpiresAt > GETUTCDATE()`.

## 7. Concurrency & Workspace Claims

- Queues are FIFO by `CreatedAt ASC`, paginated.
- Claiming an order (opening its detail screen) = atomic conditional UPDATE setting `ClaimedByUserId` + `LockedUntil` (**15 minutes**).
- **Optimistic concurrency via `RowVersion`**: simultaneous claims — first wins; the loser's `DbUpdateConcurrencyException` maps to **`409 Conflict`** (UI shows toast + invalidates TanStack cache).
- Claims auto-expire after 15 min; holders may release explicitly via `DELETE /api/orders/claim/{orderNumber}`.

## 8. Messenger Discovery Protocol

- `CustomerMessengerUsername` renders as **plaintext** with a copy-to-clipboard button.
- **No** auto-generated hyperlinks or Meta API redirect links anywhere.
- Case preservation on handles is strictly enforced at checkout intake.

## 9. Standardized Pagination

All collection endpoints support `page` (default `1`, 1-indexed) and `pageSize` (default `12`, **hard max `50`** — enforced server-side to prevent memory-exhaustion attacks). Responses wrap in `PagedResult<T>`:

```json
{
  "items": [ ... ],
  "page": 1,
  "pageSize": 12,
  "totalItems": 48,
  "totalPages": 4,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

## 10. Category Filtering

- `category` query param on product endpoints (e.g. `/api/public/products?category=flowers&page=1&pageSize=12`).
- Normalize category to **lowercase** before EF Core LINQ queries (case-insensitive match).
- Omitted/empty `category` → return all categories.
- Combines with global `IsActive = true` query filters and `AsNoTracking()`.

## 11. API Surface

| Method | Route | Auth | Notes |
| :--- | :--- | :--- | :--- |
| GET | `/api/public/products` | Anonymous | On-load cached; `category`, `page`, `pageSize` |
| POST | `/api/public/checkout/submit` | Anonymous (**rate-limited**: fixed-window per-IP, `checkout` policy) | Lazy eviction hook → recalc total → create `Pending` → return Order Number. Optional `Idempotency-Key` header: replays resolve to the original order (unique filtered index on `Orders.IdempotencyKey`, no duplicates). |
| POST | `/api/auth/login` | Anonymous | Issue JWT access + refresh pair |
| POST | `/api/auth/refresh` | Anonymous | Rotate refresh token, issue new access token |
| GET | `/api/orders/queue` | Admin / Superadmin | FIFO active `Pending`; `ProjectTo<OrderQueueDto>()`; paginated |
| POST | `/api/orders/claim/{orderNumber}` | Admin / Superadmin | 15-min workspace lock; `409` on collision |
| DELETE | `/api/orders/claim/{orderNumber}` | Admin / Superadmin | Release claim |
| POST | `/api/orders/confirm` | Admin / Superadmin | Promote to `In Production`, record payment, hitchhiker eviction |
| PATCH | `/api/admin/orders/{id}/status` | Admin / Superadmin | State machine transitions |
| GET | `/api/admin/products` | Admin / Superadmin | Includes soft-deleted via `IgnoreQueryFilters()`; paginated |
| POST | `/api/admin/products` | Admin / Superadmin | Create catalog item |
| PATCH | `/api/admin/products/{id}` | Admin / Superadmin | Update / toggle active flag |
| GET | `/api/admin/users` | **Superadmin** | List admin accounts; paginated |
| POST | `/api/admin/users` | **Superadmin** | Register Admin / Superadmin |
| DELETE | `/api/admin/users/{id}` | **Superadmin** | Soft-delete (`IsActive = false`) |

> **Note (deliberate divergence from SRS v6.0):** `POST` / `PATCH /api/admin/products` are **Admin / Superadmin** by product decision — the SRS originally listed them as "Superadmin Exclusive". Do not revert these to Superadmin-only without a requirements change.

## 12. Security & Data Integrity

- **Auth:** JWT Bearer + dual-token strategy. Refresh tokens stored as **SHA-256 hashes**; rotation enforced on renewal; logouts invalidate active tokens.
- **Centralized exception handling & uniform errors:** All unhandled exceptions flow through `GlobalExceptionHandler` (an `IExceptionHandler` registered via `AddExceptionHandler` + `UseExceptionHandler()`). It maps domain exceptions to uniform RFC 7807 `ProblemDetails`: `NotFoundException` → `404`, `UnauthorizedException` → `401`, `DuplicateEmailException` → `409`, FluentValidation `ValidationException` → `400` with a field-level `errors` dictionary, anything else → `500` (logged, generic message — never leak internals). The `ValidationFilter` short-circuit produces the identical 400 shape. **Success responses are raw DTOs (no envelope); empty actions return `204`. Controllers must never hand-craft error responses — throw a domain exception and let the handler format it.**
- **Soft deletes:** Accounts and catalog items soft-delete via `IsActive = false` + EF global query filters. Admin product listings use `IgnoreQueryFilters()` to see deleted items.
- **Email uniqueness is enforced at the DB level:** `AspNetUsers.NormalizedEmail` carries a **filtered unique index** (`EmailIndex`, `WHERE [NormalizedEmail] IS NOT NULL`, configured in `ApplicationUserConfiguration` by merging into Identity's default index). Uniqueness applies to non-null emails only — users without an email don't collide. This is belt-and-suspenders on top of `User.RequireUniqueEmail = true` (app-layer validator). **Consequence: a concurrent duplicate-email create now surfaces as `DbUpdateException` (SQL unique violation) — the future `UsersController` must catch it and map to 409/400, not rely solely on the validator.**
- **Query filter on `ApplicationUser` affects Identity & audit joins:** `UserManager.FindByEmailAsync/FindByIdAsync` inherit the `IsActive` filter, so soft-deleted admins can't authenticate (desired) — but the `UsersController` listing/reactivation endpoints MUST use `IgnoreQueryFilters()`, or reactivation can't locate the account. Also, `Payment.VerifiedBy` is a required FK whose principal can be filtered out → project audit DTOs from the raw `VerifiedByAdminId` int, not the navigation.
- **Verified order rows are immutable.**
- **Rate limiting:** Public checkout endpoints protected by ASP.NET Core **built-in** Rate Limiting middleware (`AddRateLimiter` + `UseRateLimiter`, no packages). Policy `checkout` = fixed-window per IP (`RateLimitingOptions` in `appsettings.json`); rejection → `429` with uniform `ProblemDetails` + `Retry-After` header via `OnRejected`.

## 13. Client Polling Contract (context for API design)

- Customer catalog: **on-load fetch only**, cached in memory. **Polling catalog endpoints is strictly forbidden.**
- Admin queue: TanStack Query v5 polling every **5 seconds**; support HTTP conditional headers (ETag / 304) so idle tabs don't burn bandwidth.

## 14. Hard Rules / Pitfalls to Avoid

- ❌ No background workers for eviction or any other timer-based sweeps.
- ❌ No hard deletes of order rows (including expired ones).
- ❌ No Data Annotations for EF mapping.
- ❌ No random reference codes; Order Numbers are `PFY-YYYYMMDD-XXXX` from the daily counter in `OrderRepository.CreateCheckoutAsync` (unique `OrderNumber` index = race backstop).
- ❌ Never trust client-submitted totals; always recompute from DB prices.
- ❌ No auto-links to Messenger/Meta; plaintext handles only.
- ❌ No un-paginated collection endpoints (respect the `pageSize` max of 50).
- ❌ Don't let duplicate-email `DbUpdateException` bubble up raw — `UserService` catches it and throws `DuplicateEmailException`, which `GlobalExceptionHandler` renders as `409 Conflict` (DB unique index on `NormalizedEmail`).
- ❌ No hand-crafted error responses (`ProblemDetails`/`Conflict`/`NotFound`/`Unauthorized`) in controllers — throw a domain exception (`NotFoundException`, `UnauthorizedException`, `DuplicateEmailException`) or a `ValidationException` and let `GlobalExceptionHandler` produce the uniform body.
- ❌ No `{ get; set; }` on DTOs — DTOs are immutable records (`{ get; init; }` only). To add post-mapping data (e.g., `Roles`), use a `with`-expression; never mutate a DTO property after construction.
- ✅ Always run validators before services; always wrap settlement writes in an atomic transaction.
