using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Repositories;

/// <summary>
/// Outcome of a workspace claim attempt.
/// </summary>
public enum ClaimResult
{
    Success,
    NotFound,
    NotClaimable,
    Conflict
}

/// <summary>
/// Outcome of an atomic settlement attempt.
/// </summary>
public enum SettlementResult
{
    Success,
    NotFound,
    NotClaimable,
    Conflict
}

/// <summary>
/// Outcome of a state-machine status update.
/// </summary>
public enum StatusUpdateResult
{
    Success,
    Conflict
}

/// <summary>
/// Order data access for the admin queue, workspace claims, settlement, and the
/// fulfillment state machine.
/// </summary>
public interface IOrderRepository
{
    /// <summary>Active <c>Pending</c> orders in FIFO order (SRS §2.3), paginated.</summary>
    Task<PagedResult<OrderQueueDto>> GetQueuePageAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>Every ledger order (any status), newest first, optionally filtered by status (SRS ops view).</summary>
    Task<PagedResult<OrderHistoryDto>> GetHistoryPageAsync(int page, int pageSize, string? status, CancellationToken ct);

    Task<OrderDetailDto?> GetDetailByOrderNumberAsync(string orderNumber, CancellationToken ct);

    Task<OrderDetailDto?> GetDetailByIdAsync(int id, CancellationToken ct);

    Task<string?> GetStatusByIdAsync(int id, CancellationToken ct);

    /// <summary>Acquires the 15-minute workspace claim, guarded by the read RowVersion (409 on loss).</summary>
    Task<ClaimResult> ClaimAsync(string orderNumber, int adminUserId, CancellationToken ct);

    /// <summary>Releases the claim if held by the given admin; false when not found / not held.</summary>
    Task<bool> ReleaseClaimAsync(string orderNumber, int adminUserId, CancellationToken ct);

    /// <summary>
    /// Atomically settles an order: promotes to <c>In Production</c>, writes frozen
    /// line items + payment, then runs the hitchhiker lazy eviction (SRS §3.1.4).
    /// </summary>
    Task<SettlementResult> SettleAsync(
        string orderNumber,
        int adminUserId,
        IReadOnlyList<OrderItem> items,
        decimal totalPrice,
        Payment payment,
        CancellationToken ct);

    /// <summary>Transitionally updates the status; <see cref="StatusUpdateResult.Conflict"/> on concurrent change.</summary>
    Task<StatusUpdateResult> UpdateStatusAsync(int id, string currentStatus, string newStatus, CancellationToken ct);

    /// <summary>Returns the order previously created under an idempotency key, if any.</summary>
    Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);

    /// <summary>
    /// Persists a transient <c>Pending</c> checkout (SRS §3.1.1) with a
    /// deterministic daily Order Number (<c>PFY-YYYYMMDD-XXXX</c>); the unique
    /// <c>OrderNumber</c> index backs concurrent allocation with a retry loop, and
    /// a concurrent same-key checkout resolves to the existing order (no duplicate).
    /// </summary>
    Task<Order> CreateCheckoutAsync(Order order, CancellationToken ct);

    /// <summary>
    /// Lazy eviction engine (workerless): bulk-mutates expired pending orders to
    /// <c>Expired</c>. Called on the settlement boundary (and the checkout boundary
    /// by the public flow).
    /// </summary>
    Task<int> ExecuteLazyEvictionAsync(CancellationToken ct);
}
