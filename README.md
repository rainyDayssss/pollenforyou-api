# Pollen For You Online Kiosk — Backend API

ASP.NET Core Web API (**.NET 10**) powering the Pollen For You online kiosk: customers assemble flower + coffee orders, admins manage a shared live queue with workspace claims, verify payments, and drive orders through a fulfillment state machine.

The system is built around a **unified single-ledger** design — transient checkouts and confirmed sales live in one state-driven `Orders` table — and is deliberately **workerless** to stay within Azure SQL Free Tier limits (expired orders are lazily mutated to `Expired`, never swept by background jobs).

> Full product requirements: [`SPEC.md`](SPEC.md) (SRS v6.0). Agent/developer guidance: [`AGENT.md`](AGENT.md).

---

## Features

- **Dual-token authentication** — JWT access tokens (15 min) with Identity role claims + opaque refresh tokens stored as **SHA-256 hashes**, with rotation + reuse detection on renewal and logout revocation
- **Role-based authorization** — `Admin` / `Superadmin` enforced via JWT role claims (`[Authorize(Roles=...)]`)
- **Public catalog** — on-load cached product listing with category filtering and pagination (active items only)
- **Admin inventory** — full listing including soft-deleted items, Admin / Superadmin create/update with partial (PATCH) semantics and availability toggling
- **Admin user management** — Superadmin-only account lifecycle (create / list / soft-delete / reactivate) with DB-level email uniqueness
- **Public checkout** — rate-limited customer submission: lazy-eviction hook, server-side total recomputation, deterministic `PFY-YYYYMMDD-XXXX` order numbers
- **Rate limiting** — built-in ASP.NET Core fixed-window limiter on public checkout (per-IP, 429 + `Retry-After`)
- **Live order queue** — FIFO `Pending` orders, paginated, built for 5-second TanStack polling
- **Workspace claims** — 15-minute locks guarded by `RowVersion` optimistic concurrency (loser gets `409`)
- **Atomic settlement** — one transaction: promote to `In Production`, write frozen line-item + payment snapshots, run hitchhiker lazy eviction
- **Fulfillment state machine** — forward-only transitions (`In Production → Ready for Dispatch → Dispatched → Completed`, cancellable)
- **Uniform error contract** — every error is a centralized RFC 7807 `ProblemDetails` response

## Tech Stack

| Layer | Technology |
| :--- | :--- |
| Language / Framework | C# / ASP.NET Core Web API (**.NET 10**) |
| Architecture | Layered N-Tier: Controllers → FluentValidation → Services → AutoMapper → Repositories → EF Core |
| ORM / Database | EF Core 10 (Fluent API only), SQL Server / Azure SQL (LocalDB for dev) |
| Validation | FluentValidation pipeline (global filter) |
| Mapping | AutoMapper profiles + `ProjectTo<T>()` |
| Identity / Auth | ASP.NET Core Identity + JWT Bearer + SHA-256 hashed refresh tokens |
| CI/CD (target) | GitHub Actions → Azure App Service (F1 Free Tier) |

## Architecture

```
HTTP Request
    │
    ▼
Controllers (presentation) ──► GlobalExceptionHandler (uniform errors)
    │
    ▼
FluentValidation (ValidationFilter — 400 on failure)
    │
    ▼
Services (domain logic: prices recomputed server-side, transitions validated)
    │
    ▼
Repositories (EF Core data access — AsNoTracking reads, IgnoreQueryFilters where needed)
    │
    ▼
PfyDbContext ──► SQL Server / Azure SQL
```

Key conventions (all enforced in [AGENT.md](AGENT.md)):

- **DTOs are immutable records** (`{ get; init; }`) — post-mapping enrichment uses `with`-expressions
- **Client-submitted prices are never trusted** — totals are always recomputed from DB `BasePrice`
- **Reads are `AsNoTracking()`** + paginated (page size hard-capped at **50**)
- **Soft deletes** via `IsActive` global query filters; admin views bypass with `IgnoreQueryFilters()`
- **Controllers never hand-craft error responses** — they throw and let the central handler format them

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [EF Core tools](https://learn.microsoft.com/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef` (required for `dotnet ef` commands)
- SQL Server — SQL Server Express **LocalDB** (default dev connection) or any SQL Server instance / Azure SQL Database

## Getting Started

```bash
# 1. Restore packages
dotnet restore

# 2. Create the database from migrations (uses the DefaultConnection string)
dotnet ef database update

# 3. Run the API
dotnet run
```

- HTTP profile: `http://localhost:5004`
- HTTPS profile: `https://localhost:7200` (+ `http://localhost:5004`)
- OpenAPI (development only): `http://localhost:5004/openapi/v1.json`

The API is pre-seeded with the operational roles (`Admin`, `Superadmin`) and a **default superadmin account** (see below).

## Configuration

Configuration lives in `appsettings.json` and can be overridden with environment variables / app settings in production.

| Key | Default (dev) | Purpose |
| :--- | :--- | :--- |
| `ConnectionStrings:DefaultConnection` | LocalDB `PollenForYouDb` | SQL Server connection string |
| `Jwt:Key` | dev-only signing key | HS256 signing key — **rotate before production** |
| `Jwt:Issuer` / `Jwt:Audience` | `PollenForYouApi` / `PollenForYouAdmin` | Token issuer/audience |
| `Jwt:AccessTokenLifetimeMinutes` | `15` | Access-token lifetime |
| `Jwt:RefreshTokenLifetimeDays` | `7` | Refresh-token session lifetime |
| `DefaultAdmin:Email` | `superadmin@pollenforyou.com` | Seeded superadmin email |
| `DefaultAdmin:Password` | `Superadmin@2026` | Seeded superadmin password |
| `RateLimiting:CheckoutPermitLimit` | `10` | Max checkout submissions per IP per window |
| `RateLimiting:CheckoutWindowSeconds` | `60` | Fixed-window length for checkout limiting |
| `Cors:AllowedOrigins` | `localhost:5173`, `localhost:3000` | Frontend origins allowed cross-origin (set to your Vercel URL in production) |
| `AllowedHosts` | `*` (dev) / `api.pollenforyou.com` (prod) | Host-header allow-list — hardened in `appsettings.Production.json` |

### Default superadmin

The startup seeder (`DbInitializer`) idempotently creates the default superadmin if it doesn't exist:

```
Email:    superadmin@pollenforyou.com
Password: Superadmin@2026
```

> ⚠️ **Development only.** Rotate `Jwt:Key` and override `DefaultAdmin` via environment variables before any production deployment.

## Authentication Flow

1. `POST /api/auth/login` with an admin's email + password → `{ accessToken, refreshToken, expiresInSeconds, email, roles }`
2. Send the access token as `Authorization: Bearer <accessToken>` on protected endpoints
3. When the access token nears expiry, `POST /api/auth/refresh` with the refresh token → a **new pair** (the old refresh token is rotated/consumed)
4. `POST /api/auth/logout` revokes all active refresh sessions for the authenticated admin

Refresh tokens are never stored in plaintext — only their SHA-256 hash. Replaying a rotated/revoked token is detected and kills the whole token family.

## API Reference

All implemented endpoints. `page` (default `1`) and `pageSize` (default `12`, max `50`) paginate every collection.

### Public (anonymous)

| Method | Route | Description |
| :--- | :--- | :--- |
| `GET` | `/health` | Liveness/readiness probe — verifies DB connectivity (for App Service / load balancers) |
| `GET` | `/api/public/products` | Active products, optional `?category=flowers&page=1&pageSize=12` |
| `POST` | `/api/public/checkout/submit` | Customer checkout (**rate-limited**): returns Order Number; optional `Idempotency-Key` header makes retries safe |
| `POST` | `/api/auth/login` | Issue JWT access + refresh pair |
| `POST` | `/api/auth/refresh` | Rotate refresh token, issue new pair |

### Authenticated (Admin / Superadmin)

| Method | Route | Description |
| :--- | :--- | :--- |
| `POST` | `/api/auth/logout` | Revoke all active refresh sessions |
| `GET` | `/api/orders/queue` | FIFO active `Pending` orders (paginated); supports **ETag / `If-None-Match` → `304`** for 5s polling (client opt-in) |
| `POST` | `/api/orders/claim/{orderNumber}` | Acquire 15-min workspace claim (`409` on collision) |
| `DELETE` | `/api/orders/claim/{orderNumber}` | Release your claim |
| `POST` | `/api/orders/confirm` | Settle: promote to `In Production`, write frozen items + payment |
| `PATCH` | `/api/admin/orders/{id}/status` | Fulfillment transitions (`Ready for Dispatch`, `Dispatched`, `Completed`, `Cancelled`) |
| `GET` | `/api/admin/products` | Full inventory incl. soft-deleted; `category`, `page`, `pageSize` |
| `POST` | `/api/admin/products` | Create catalog item |
| `PATCH` | `/api/admin/products/{id}` | Partial update / toggle `isActive` |

### Superadmin only

| Method | Route | Description |
| :--- | :--- | :--- |
| `GET` | `/api/admin/users` | List admin accounts (incl. soft-deleted) |
| `POST` | `/api/admin/users` | Register `Admin` / `Superadmin` account |
| `PATCH` | `/api/admin/users/{id}/reactivate` | Reactivate a soft-deleted account |
| `DELETE` | `/api/admin/users/{id}` | Soft-delete an account |

### Example: login + fetch the queue

> The pipeline redirects HTTP → HTTPS, so call the HTTPS profile. The dev certificate is self-signed, so `curl` needs `-k` (or use Postman/your HTTP client's trusted-cert handling).

```bash
# 1. Login (default superadmin)
curl -sk https://localhost:7200/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@pollenforyou.com","password":"Superadmin@2026"}'

# 2. Use the returned accessToken
curl -sk https://localhost:7200/api/orders/queue \
  -H "Authorization: Bearer <accessToken>"
```

## Error Handling & Response Contract

**Success responses are raw payloads** (no envelope). Empty actions (`DELETE`, `logout`) return `204 No Content`.

**Every error is a uniform RFC 7807 `ProblemDetails`** produced by `GlobalExceptionHandler` (`IExceptionHandler` + `UseExceptionHandler()`). (One exception: authentication-layer `401`/`403` from the JWT bearer middleware — missing/expired token on a protected endpoint — use the framework's standard challenge response, not `ProblemDetails`.)

```json
// 400 — validation (field-level errors)
{
  "type": "about:blank",
  "title": "Validation Failed",
  "status": 400,
  "detail": "Validation failed.",
  "errors": { "Email": ["Email is required."] }
}

// 404 — not found
{ "type": "about:blank", "title": "Not Found", "status": 404, "detail": "Order with id 42 was not found." }
```

| HTTP Status | Raised for |
| :--- | :--- |
| `400` | FluentValidation failures (request boundary + service-level), illegal status transitions, inactive products on settlement |
| `401` | Bad credentials, invalid/expired/revoked refresh token |
| `404` | Missing order/user/product, release of an unclaimed order |
| `409` | Duplicate email / product code, claim collisions (`RowVersion` loss), settlement conflicts |
| `429` | Rate limit exceeded on public checkout (includes `Retry-After`) |
| `500` | Unhandled exceptions (logged; generic message, no internals leaked) |

## Order Lifecycle

```
Pending ───► In Production ───► Ready for Dispatch ───► Dispatched ───► Completed
   │
   ├───► Expired   (automatic lazy mutation after 2 hours, workerless)
   └───► Cancelled (manual admin rejection)
```

- Checkouts are created `Pending` with a 2-hour `ExpiresAt` TTL and a structured `PFY-YYYYMMDD-XXXX` order number
- Optional `Idempotency-Key` header on checkout — the same key always resolves to the same order (no duplicates on retry/double-click)
- Settling an order requires it to be **pending, unexpired, and claimed by the settling admin**
- Settlement freezes line-item snapshots (product name + price-at-purchase) for financial auditability
- Expired records are never deleted — reads isolate them with `WHERE Status = 'Pending' AND ExpiresAt > GETUTCDATE()`

## Project Structure

```
PollenForYouApi/
├── Controllers/          # Auth, Users, Catalog, Checkout, Products, AdminOrders
├── DTOs/                 # Immutable record contracts
├── Entities/             # Domain models + status/role/stage constants
├── Validators/           # FluentValidation validators
├── Services/             # Domain logic (Auth, User, Product, Order)
├── Repositories/         # EF Core data access
├── Profiles/             # AutoMapper profiles
├── Filters/              # ValidationFilter (global FluentValidation pipeline)
├── Middleware/           # GlobalExceptionHandler (centralized errors)
├── Exceptions/           # Domain exceptions (404 / 401 / 409)
├── Data/                 # PfyDbContext, Fluent API configurations, DbInitializer seed
├── Migrations/           # EF Core migrations
├── Options/              # Strongly-typed JwtOptions
└── Program.cs            # Composition root / DI / pipeline
```

## CORS & Production Hardening

- **CORS** is config-driven: `Cors:AllowedOrigins` in `appsettings.json` (dev) / `appsettings.Production.json` (prod). The React frontend (Vercel in production) must be listed here or its browser requests will be blocked. An empty list = deny all cross-origin (secure default).
- **Health checks**: `GET /health` runs a dependency-free DB connectivity probe (custom `DatabaseHealthCheck` using the existing `PfyDbContext` — no extra packages). Anonymous by design for load-balancer probes.
- **`AllowedHosts`** is restricted to `api.pollenforyou.com` in `appsettings.Production.json` — set the real API hostname before deploying. Dev keeps `*` for local flexibility.

## Roadmap / Not Yet Implemented

- Frontend client (React 19 / TypeScript, TanStack Query v5)
- Post-MVP (deferred by SRS): Meta webhook ingestion, Hugging Face demand forecasting
