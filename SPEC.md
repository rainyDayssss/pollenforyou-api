# Software Requirements Specification (SRS)[cite: 1]

**Project Name:** Pollen For You Online Kiosk[cite: 1]  
**Version:** 6.0 (Unified Single-Ledger State Machine Architecture)[cite: 1]  
**Date:** July 2026[cite: 1]  
**Changelog from v5.6:** Replaced random 6-character reference codes with deterministic structured order numbers (`PFY-YYYYMMDD-XXXX`). Transitioned transient eviction from hard row deletes to lazy state mutation (`Status = 'Expired'`). Upgraded authentication to a dual-token strategy with SHA-256 hashed refresh token storage. Enforced 15-minute workspace claim locks (`ClaimedByUserId`, `LockedUntil`) alongside optimistic concurrency tokens (`RowVersion`). Integrated FluentValidation for request DTO validation, AutoMapper for object projection, and EF Core Fluent API for relational database design.[cite: 1]

---

## 1. Introduction[cite: 1]

### 1.1 Purpose[cite: 1]
This document details the definitive software and operational requirements for the Pollen For You Online Kiosk MVP. The design maximizes infrastructure stability on Azure SQL Free Tier by consolidating transient checkouts and confirmed sales into a single, state-driven relational ledger. The architecture strictly partitions client connection strategies, guarantees financial auditability via frozen snapshots, and eliminates multi-operator race conditions using hybrid locking mechanisms.[cite: 1]

### 1.2 Scope[cite: 1]
Pollen For You allows customers to assemble flower and coffee orders via a public web application. The customer completes a checkout form—including delivery details and their raw Facebook Messenger username—and submits it to the API. The server validates pricing server-side, generates a structured Order Number (e.g., `PFY-20260731-0001`), creates a `Pending` order record, and returns a confirmation payload.[cite: 1]

Administrators monitor a shared live queue, claim pending orders via a 15-minute workspace lock, copy customer handles to their system clipboard, and manually locate user profiles in standalone Facebook Messenger applications. Upon verifying payment details, admins promote orders to `In Production` and record financial transaction logs. Unverified orders naturally expire after 2 hours via lazy state transitions without background worker dependencies.[cite: 1]

> **Post-MVP Integration Notes:** Automated Meta Webhook ingestion and Hugging Face demand forecasting integrations are explicitly deferred and will be layered onto this single-ledger schema in a future release without altering baseline integration protocols.[cite: 1]

---

## 2. Overall Description[cite: 1]

### 2.1 Product Perspective[cite: 1]
The solution is an asymmetric decoupled architecture maximizing runtime resource efficiency inside cost-effective host servers while preserving clean data quality for future forecasting integration.[cite: 1]

* **Frontend Client:** React 19 (TypeScript) compiled as standalone static builds optimized for Vercel edge deployment targets.[cite: 1]
* **Continuous Integration / Deployment:** **GitHub Actions CI/CD Pipeline** automatically compiles source code, executes linters and unit tests, and ships the backend application directly to Azure App Service instances.[cite: 1]
* **Backend Application:** ASP.NET Core Web API (.NET 10, C#) structured around a classic **Layered N-Tier Architecture** using **Controllers** (Presentation Layer), **FluentValidation** (Input Validation Layer), **Services** (Domain Logic Layer), **AutoMapper Profiles** (Data Mapping Layer), **Repositories** (Data Access Layer), **DTOs** (Data Contracts), and **Domain Entities**.[cite: 1]
* **Identity & Auth Layer:** **ASP.NET Core Identity Core** backed by secure token hashing for session rotation.[cite: 1]
* **Data Architecture:** SQL Server / Azure SQL Database Free Tier Instance managed by Entity Framework Core 10, configured strictly via **EF Core Fluent API** inside `DbContext.OnModelCreating()`.[cite: 1]

### 2.2 User Classes & Roles[cite: 1]
* **Customer:** Public, unauthenticated access. Functions: load product catalog, configure cart, fill out delivery details, and submit checkout to receive an Order Number.[cite: 1]
* **Admin:** Authenticated worker identity. Functions: monitor live queue, claim pending orders, manually search customer handles in Messenger, verify payments, and promote orders into production.[cite: 1]
* **Superadmin:** Master identity. Functions: all Admin capabilities (product catalog management included), plus account lifecycle control over administrative credentials.[cite: 1]

### 2.3 Data Communication & Polling Strategy[cite: 1]
1. **Customer Catalog Fetching:** Employs **On-Load Fetching** with optional category filtering. Single HTTP `GET` cached in React memory. Polling catalog endpoints is strictly forbidden.[cite: 1]
2. **Admin Queue Syncing:** Managed via **TanStack Query v5** querying the queue endpoint every 5 seconds. Queries execute as `AsNoTracking()` reads in the repository layer. Read queries enforce `WHERE Status = 'Pending' AND ExpiresAt > GETUTCDATE()` to isolate active orders. HTTP Conditional Headers (ETags/304 Not Modified) pause network load on idle tabs.[cite: 1]

### 2.4 Standardized Pagination Strategy[cite: 1]
To preserve database memory on Azure SQL Free Tier and optimize network payloads, all collection endpoints support limit-offset pagination via standard URL query parameters:[cite: 1]

* `page` (integer, default: `1`): The current requested page number (1-indexed).[cite: 1]
* `pageSize` (integer, default: `12`, max: `50`): The number of records returned per page. The server enforces a maximum limit of 50 to prevent memory exhaustion attack vectors.[cite: 1]

Paginated endpoints wrap payload responses in a standardized `PagedResult<T>` envelope:[cite: 1]

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
```[cite: 1]

In Entity Framework Core repositories, pagination is executed at the database level using SQL `OFFSET...FETCH NEXT` via LINQ `.Skip((page - 1) * pageSize).Take(pageSize)`.[cite: 1]

### 2.5 Category Filtering via Query Parameters[cite: 1]
Product catalog endpoints support optional filtering by product categories using the `category` query parameter (e.g., `/api/public/products?category=flowers` or `/api/public/products?category=coffee&page=1&pageSize=12`).[cite: 1]

* **Case-Insensitive Matching:** The backend normalizes category strings to lowercase prior to executing EF Core LINQ queries.[cite: 1]
* **Optional Filter:** When `category` is omitted or empty, the API returns products across all categories.[cite: 1]
* **EF Core Integration:** Category filters combine dynamically with global soft-delete query filters (`IsActive = true`) and non-tracking read optimizations (`AsNoTracking()`).[cite: 1]

### 2.6 Backend Validation, Mapping & Modeling Framework[cite: 1]
* **FluentValidation:** All inbound HTTP request DTOs are validated using FluentValidation pipeline filters before entering application services. Requests failing validation instantly return standard `400 Bad Request` validation problem details.[cite: 1]
* **AutoMapper Profiles:** Projection between database entities, internal service models, and API response DTOs is configured via dedicated AutoMapper Profiles (e.g., `OrderMappingProfile`, `ProductMappingProfile`). Projection on queries leverages `ProjectTo<T>()` for SQL query generation efficiency.[cite: 1]
* **EF Core Fluent API Design:** Relational entity mapping eschews Data Annotations in favor of clean `IEntityTypeConfiguration<T>` classes configured inside `DbContext.OnModelCreating()`. Explicit configurations define table names, primary keys, required string lengths, foreign key constraints, `RowVersion` concurrency tokens, and global soft-delete query filters.[cite: 1]

---

## 3. Specific Requirements[cite: 1]

### 3.1 Order Lifecycle & State Machine Specification[cite: 1]
Orders follow a strict transition graph. Transient checkouts that are unverified naturally transition to `Expired` via lazy state mutation rather than hard row deletion.[cite: 1]

```text
Pending ───► In Production ───► Ready for Dispatch ───► Dispatched ───► Completed
   │
   ├───► Expired (Automatic Lazy State Mutation after 2 Hours)
   └───► Cancelled (Manual Admin Rejection)
```[cite: 1]

#### 3.1.1 Public Customer Checkout[cite: 1]
* Cart configuration and calculations occur strictly in local client memory.[cite: 1]
* Checkout collects: Customer Name, Facebook Messenger Username, Receiver Name, Receiver Contact Number, Delivery Address, Delivery Date, Booking Time, and optional Card Message. Inbound DTO is validated via `CheckoutRequestDtoValidator`.[cite: 1]
* The backend executes an instant bulk lazy eviction mutation (updating unverified expired rows to `Status = 'Expired'`) inside `OrderRepository.ExecuteLazyEvictionAsync()` before creating new records.[cite: 1]
* The service layer recalculates totals server-side using base prices fetched directly from the database, discarding client-submitted pricing. AutoMapper projects validated requests into domain entities.[cite: 1]
* On success, the service generates a structured Order Number formatted as `PFY-YYYYMMDD-XXXX` (e.g., `PFY-20260731-0001`) using a daily database sequence, assigning `Status = 'Pending'` and `ExpiresAt = DateTime.UtcNow.AddHours(2)`.[cite: 1]
* The Controller returns a `CheckoutResponseDto` containing the Order Number to the customer.[cite: 1]

#### 3.1.2 Concurrency Locks & Workspace Claim Management[cite: 1]
* Admin queues display active orders sorted by `CreatedAt ASC` (FIFO), paginated using standard query parameters.[cite: 1]
* Viewing an order detail screen calls `AdminOrdersController.ClaimOrder()`. The service layer executes an atomic conditional update setting workspace locks for 15 minutes.[cite: 1]
* Sub-millisecond race conditions between admins are guarded by SQL Server `RowVersion` tokens configured via Fluent API (`builder.Property(o => o.RowVersion).IsRowVersion()`). Losing requests receive `409 Conflict`, triggering a UI toast notification and a queue cache invalidation via TanStack Query.[cite: 1]
* Claims automatically expire after 15 minutes. Admins holding active claims may explicitly release them via a `DELETE` claim endpoint call.[cite: 1]

#### 3.1.3 Manual Messenger Discovery Protocol[cite: 1]
* The Admin UI renders customer `CustomerMessengerUsername` values as plaintext alongside a copy-to-clipboard button.[cite: 1]
* The system prohibits automated hyperlinks or Meta API redirect links.[cite: 1]
* Operators manually copy the username, open standalone Facebook Messenger applications, and locate customer profiles via manual search. Case preservation on handles is strictly enforced by the backend during checkout intake.[cite: 1]

#### 3.1.4 Order Confirmation & Financial Settlement[cite: 1]
* When payment is verified, `OrderRepository` executes the following inside an atomic database transaction:[cite: 1]
  1. Updates the target order record: sets `Status = 'In Production'`, populates `SettledByAdminId`, and clears workspace claims.[cite: 1]
  2. Writes order items rows with frozen snapshots (product name and purchase price).[cite: 1]
  3. Writes a payment entity recording stage (`Downpayment`, `Full Payment`), method, amount, and reference code.[cite: 1]
  4. Executes a hitchhiker lazy eviction query sweeping away all unrelated expired pending records.[cite: 1]
* Fulfillment state transitions proceed through: `In Production`, `Ready for Dispatch`, `Dispatched`, `Completed`, or `Cancelled`.[cite: 1]

#### 3.1.5 Lazy Eviction Engine[cite: 1]
* The system operates entirely workerless to protect Azure Free Tier CPU/connection thresholds.[cite: 1]
* Eviction is triggered on-demand via bulk updates that set `Status = 'Expired'` on rows where `Status == 'Pending' AND ExpiresAt <= DateTime.UtcNow`:[cite: 1]
  1. **Entry Boundary Trigger:** Runs during Customer Checkout submission.[cite: 1]
  2. **Settlement Boundary Trigger:** Runs inside Admin Order Settlement transactions.[cite: 1]
* During store off-hours, expired records remain logically isolated by `WHERE Status = 'Pending' AND ExpiresAt > GETUTCDATE()` filters on read operations.[cite: 1]

---

## 4. Non-Functional Requirements & Security[cite: 1]
* **Read Optimization:** All queue and catalog queries invoke `AsNoTracking()`, append logical expiry or query filters, and apply SQL pagination limits. AutoMapper's `.ProjectTo<T>()` optimizes SQL generation.[cite: 1]
* **Validation Integrity:** FluentValidation handles all request boundary checks before controller action execution.[cite: 1]
* **Authentication:** JWT Bearer authentication paired with SHA-256 token hashes. Refresh token rotation is enforced upon renewal; logouts invalidate active tokens.[cite: 1]
* **Data Integrity & Soft Deletes:** Soft deletes (`IsActive = false`) are enforced on internal accounts and catalog items. EF Core Fluent API configures global query filters (`HasQueryFilter(e => e.IsActive)`) to isolate deleted rows automatically. Verified order entries are immutable.[cite: 1]
* **Rate Limiting:** Public checkout endpoints are governed by ASP.NET Core Rate Limiting middleware.[cite: 1]

---

## 5. Technical Stack Summary[cite: 1]

| Layer | Technology |
| :--- | :--- |
| **CI/CD Lifecycle** | **GitHub Actions Pipeline** (Automated builds, unit tests, and cloud deployments) |[cite: 1]
| Client Language & Framework | TypeScript, React 19, Vite |[cite: 1]
| Client Data State | **TanStack Query v5** (Caching, conditional headers, focus polling loops) |[cite: 1]
| Server Language & Architecture | C# (.NET 10), **Layered N-Tier Architecture** (Controllers, Services, Repositories, DTOs, Entities) |[cite: 1]
| Input Validation & Mapping | **FluentValidation** (DTO Validation) & **AutoMapper** (Object Projection) |[cite: 1]
| Identity & Auth | **ASP.NET Core Identity Core** with SHA-256 Hashed Refresh Token Storage |[cite: 1]
| ORM & Database Design | EF Core 10 (**Fluent API Configuration**), Azure SQL Database / SQL Server (Unified Single Ledger, EF Global Query Filters, Bulk Lazy Eviction, Paginated Server Reads) |[cite: 1]
| Hosting Targets | Vercel (Frontend Static Edge) / **Azure App Service F1 Free Tier** (Backend Web API) |[cite: 1]

---

## 6. End-to-End Execution Flow Blueprint[cite: 1]

### Single-Ledger Lifecycle & Concurrency Collision Resolution[cite: 1]

```mermaid
sequenceDiagram
    autonumber
    actor Cust as Customer
    actor AdminA as Sarah (Admin A)
    actor AdminB as John (Admin B)
    participant Controller as API Controllers
    participant Validator as FluentValidation
    participant Mapper as AutoMapper
    participant Service as Order Service
    participant Repo as Order Repository
    participant DB as Azure SQL Database

    Note over Cust, DB: Phase 1 — Customer Checkout, Validation & Lazy Eviction Hook
    Cust->>Controller: POST /api/public/checkout/submit (CheckoutRequestDto)
    Controller->>Validator: ValidateAsync(CheckoutRequestDto)
    Validator-->>Controller: Validation Success
    Controller->>Service: SubmitCheckoutAsync(dto)
    Service->>Repo: ExecuteLazyEvictionAsync()
    Repo->>DB: UPDATE Orders SET Status='Expired' WHERE Status='Pending' AND ExpiresAt <= UtcNow
    Note over DB: Mutates old abandoned checkouts to 'Expired'
    Service->>Mapper: Map<Order>(dto)
    Mapper-->>Service: Domain Entity Order
    Service->>Repo: CreateOrderAsync(Order)
    Repo->>DB: INSERT Order via Fluent API mappings (PFY-20260731-0001, Status='Pending', RowVersion: 0xAAA1)
    Controller-->>Cust: Order Number PFY-20260731-0001 displayed

    Note over AdminA, Controller: Phase 2 — Dashboard Queue Polling (Paginated)
    AdminA->>Controller: GET /api/orders/queue?page=1&pageSize=10 (5s TanStack Poll)
    AdminB->>Controller: GET /api/orders/queue?page=1&pageSize=10 (5s TanStack Poll)
    Controller->>Service: GetActiveQueueAsync(page=1, pageSize=10)
    Service->>Repo: GetActiveQueueAsync(1, 10)
    Repo->>DB: Fetch via AsNoTracking().ProjectTo<OrderQueueDto>().Skip(0).Take(10) WHERE Status='Pending' AND ExpiresAt > GETUTCDATE()
    DB-->>Repo: Return Queue Active List Page
    Repo-->>Service: Return DTOs
    Service-->>Controller: Return PagedResult<OrderQueueDto>
    Controller-->>AdminA: Populate Dashboard Queue UI
    Controller-->>AdminB: Populate Dashboard Queue UI

    Note over AdminA, AdminB: Phase 3 — Workspace Lock Race Condition
    AdminA->>Controller: POST /api/orders/claim/PFY-20260731-0001
    AdminB->>Controller: POST /api/orders/claim/PFY-20260731-0001
    Controller->>Service: ClaimOrderAsync('PFY-20260731-0001', adminA_Id)
    Controller->>Service: ClaimOrderAsync('PFY-20260731-0001', adminB_Id)

    Note over Service: Sarah's claim processes first
    Service->>Repo: CommitClaimAsync(order, adminA_Id)
    Repo->>DB: UPDATE Orders SET ClaimedByUserId=1, LockedUntil=+15m WHERE OrderNumber=... AND RowVersion=0xAAA1
    DB-->>Repo: 1 row affected (Success — RowVersion updates to 0xAAA2)
    Controller-->>AdminA: 200 OK (Sarah opens workspace panel)

    Note over Service: John's claim processes second
    Service->>Repo: CommitClaimAsync(order, adminB_Id)
    Repo->>DB: UPDATE Orders SET ClaimedByUserId=2 WHERE OrderNumber=... AND RowVersion=0xAAA1
    Note over DB: Fails: RowVersion is now 0xAAA2
    DB-->>Repo: DbUpdateConcurrencyException
    Repo-->>Service: Catch Exception & return false
    Controller-->>AdminB: 409 Conflict Response
    AdminB->>Controller: Invalidate Cache -> Re-fetch queue
    Controller-->>AdminB: Updated Queue (Row marked 'Claimed by Sarah')

    Note over AdminA: Sarah copies Messenger handle, opens standalone app, searches customer, and verifies payment.

    Note over AdminA, DB: Phase 5 — Ledger Settlement & Hitchhiker Eviction
    AdminA->>Controller: POST /api/orders/confirm (OrderConfirmationDto)
    Controller->>Validator: ValidateAsync(OrderConfirmationDto)
    Validator-->>Controller: Validation Success
    Controller->>Service: ConfirmOrderSettlementAsync(dto, adminA_Id)
    Service->>Repo: SettleOrderTransactionAsync(...)
    Note over Repo: Opens Atomic Database Transaction Block
    Repo->>DB: UPDATE Orders SET Status='In Production', SettledByAdminId=1
    Repo->>DB: INSERT OrderItems, Payments
    Repo->>DB: UPDATE Orders SET Status='Expired' WHERE Status='Pending' AND ExpiresAt <= UtcNow
    Note over Repo: Transaction Committed
    Controller-->>AdminA: 200 OK — Order promoted to 'In Production'

    Note over AdminA, DB: Phase 6 — Physical Lifecycle Transitions
    AdminA->>Controller: PATCH /api/admin/orders/{id}/status ('Ready for Dispatch')
    AdminA->>Controller: PATCH /api/admin/orders/{id}/status ('Dispatched')
    AdminA->>Controller: PATCH /api/admin/orders/{id}/status ('Completed')
    Note over DB: Permanent Ledger state updated.
```[cite: 1]

---

## 7. API Endpoint Reference[cite: 1]

| Method | Route | Controller | Auth Access Policy | Description / Feature Handler |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/public/products` | `CatalogController` | Anonymous (Public) | Serves product listings via on-load caching. Supports `category`, `page`, and `pageSize` query parameters (e.g. `?category=flowers&page=1&pageSize=12`). Filtered by EF Query Filters, mapped via AutoMapper. |[cite: 1]
| `POST` | `/api/public/checkout/submit` | `CheckoutController` | Anonymous (Rate Limited) | Validated via `CheckoutRequestValidator`. Executes lazy eviction hook, recalculates total, creates `Pending` order, returns Order Number. |[cite: 1]
| `POST` | `/api/auth/login` | `AuthController` | Anonymous (Identity) | Verifies administrative credentials, issues JWT Access Token & Refresh Token pair. Validated via FluentValidation. |[cite: 1]
| `POST` | `/api/auth/refresh` | `AuthController` | Anonymous (Identity) | Rotates Refresh Token and issues new JWT Access Token. |[cite: 1]
| `GET` | `/api/orders/queue` | `AdminOrdersController` | Admin / Superadmin | Managed via TanStack Query. Fetches active `Pending` orders via FIFO using AutoMapper `ProjectTo<OrderQueueDto>()`. Supports `page` and `pageSize` query parameters. |[cite: 1]
| `POST` | `/api/orders/claim/{orderNumber}` | `AdminOrdersController` | Admin / Superadmin | Executes 15-minute workspace claim lock. Fails with 409 Conflict on collision. |[cite: 1]
| `DELETE` | `/api/orders/claim/{orderNumber}` | `AdminOrdersController` | Admin / Superadmin | Releases active workspace claim lock. |[cite: 1]
| `POST` | `/api/orders/confirm` | `AdminOrdersController` | Admin / Superadmin | Validated via `OrderConfirmationValidator`. Promotes order to `In Production`, records payment, and runs hitchhiker lazy eviction. |[cite: 1]
| `PATCH` | `/api/admin/orders/{id}/status` | `AdminOrdersController` | Admin / Superadmin | Updates order state machine status (Ready for Dispatch, Dispatched, Completed, Cancelled). |[cite: 1]
| `GET` | `/api/admin/products` | `ProductsController` | Admin / Superadmin | Returns inventory including soft-deleted items via `IgnoreQueryFilters()`. Supports `category`, `page`, and `pageSize` query params. Mapped via AutoMapper. |[cite: 1]
| `POST` | `/api/admin/products` | `ProductsController` | Admin / Superadmin | Creates new catalog item. Validated via FluentValidation. |[cite: 1]
| `PATCH` | `/api/admin/products/{id}` | `ProductsController` | Admin / Superadmin | Updates catalog item or toggles active availability flag. Validated via FluentValidation. |[cite: 1]
| `GET` | `/api/admin/users` | `UsersController` | Superadmin Exclusive | Lists administrative user accounts. Supports `page` and `pageSize` query params. |[cite: 1]
| `POST` | `/api/admin/users` | `UsersController` | Superadmin Exclusive | Registers new Admin or Superadmin user. Validated via FluentValidation. |[cite: 1]
| `DELETE` | `/api/admin/users/{id}` | `UsersController` | Superadmin Exclusive | Soft-deletes administrative user account (`IsActive = false`). |[cite: 1]