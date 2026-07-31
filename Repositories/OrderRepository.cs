using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PollenForYouApi.Data;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;
using PollenForYouApi.Exceptions;

namespace PollenForYouApi.Repositories;

/// <summary>
/// EF Core data access for the unified single ledger. Claims use optimistic
/// concurrency (RowVersion read + conditional update); settlement runs inside an
/// atomic transaction; status transitions and eviction use <c>ExecuteUpdateAsync</c>.
/// </summary>
public class OrderRepository : IOrderRepository
{
    private const int ClaimDurationMinutes = 15;

    private readonly PfyDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(PfyDbContext db, IMapper mapper, ILogger<OrderRepository> logger)
    {
        _db = db;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResult<OrderQueueDto>> GetQueuePageAsync(int page, int pageSize, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var query = _db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatuses.Pending && o.ExpiresAt > now);

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<OrderQueueDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return new PagedResult<OrderQueueDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1
        };
    }

    public async Task<OrderDetailDto?> GetDetailByOrderNumberAsync(string orderNumber, CancellationToken ct)
    {
        return await _db.Orders
            .AsNoTracking()
            .Where(o => o.OrderNumber == orderNumber)
            .ProjectTo<OrderDetailDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<OrderDetailDto?> GetDetailByIdAsync(int id, CancellationToken ct)
    {
        return await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .ProjectTo<OrderDetailDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> GetStatusByIdAsync(int id, CancellationToken ct)
    {
        return await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => o.Status)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ClaimResult> ClaimAsync(string orderNumber, int adminUserId, CancellationToken ct)
    {
        // Read the order (and its RowVersion token) for the claim attempt.
        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);

        if (order is null)
        {
            return ClaimResult.NotFound;
        }

        var now = DateTime.UtcNow;

        if (order.Status != OrderStatuses.Pending || order.ExpiresAt <= now)
        {
            return ClaimResult.NotClaimable;
        }

        if (order.ClaimedByUserId is not null && order.LockedUntil > now)
        {
            return ClaimResult.Conflict;
        }

        // Optimistic concurrency: a concurrent claim between the read and this
        // update either bumps RowVersion or satisfies none of the claim conditions,
        // so the conditional UPDATE affects 0 rows → the loser gets 409.
        var affected = await _db.Orders
            .Where(o => o.Id == order.Id
                && o.RowVersion == order.RowVersion
                && o.Status == OrderStatuses.Pending
                && o.ExpiresAt > now
                && (o.ClaimedByUserId == null || o.LockedUntil <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.ClaimedByUserId, adminUserId)
                .SetProperty(o => o.LockedUntil, now.AddMinutes(ClaimDurationMinutes)), ct);

        return affected > 0 ? ClaimResult.Success : ClaimResult.Conflict;
    }

    public async Task<bool> ReleaseClaimAsync(string orderNumber, int adminUserId, CancellationToken ct)
    {
        var affected = await _db.Orders
            .Where(o => o.OrderNumber == orderNumber && o.ClaimedByUserId == adminUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.ClaimedByUserId, (int?)null)
                .SetProperty(o => o.LockedUntil, (DateTime?)null), ct);

        return affected > 0;
    }

    public async Task<SettlementResult> SettleAsync(
        string orderNumber,
        int adminUserId,
        IReadOnlyList<OrderItem> items,
        decimal totalPrice,
        Payment payment,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Validate the order state BEFORE opening the transaction so the doomed
        // (not-found / not-claimable) paths never acquire one. The conditional
        // UPDATE inside the transaction re-validates atomically and remains the
        // real guard against concurrent changes.
        var order = await _db.Orders
            .AsNoTracking()
            .Where(o => o.OrderNumber == orderNumber)
            .Select(o => new { o.Id, o.Status, o.ExpiresAt, o.ClaimedByUserId })
            .FirstOrDefaultAsync(ct);

        if (order is null)
        {
            return SettlementResult.NotFound;
        }

        if (order.Status != OrderStatuses.Pending || order.ExpiresAt <= now || order.ClaimedByUserId != adminUserId)
        {
            return SettlementResult.NotClaimable;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // 1. Promote the order and clear the workspace claim (SRS §3.1.4).
        var affected = await _db.Orders
            .Where(o => o.Id == order.Id
                && o.Status == OrderStatuses.Pending
                && o.ExpiresAt > now
                && o.ClaimedByUserId == adminUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, OrderStatuses.InProduction)
                .SetProperty(o => o.SettledByAdminId, adminUserId)
                .SetProperty(o => o.ClaimedByUserId, (int?)null)
                .SetProperty(o => o.LockedUntil, (DateTime?)null)
                .SetProperty(o => o.TotalPrice, totalPrice), ct);

        if (affected == 0)
        {
            return SettlementResult.Conflict;
        }

        // 2. Write the frozen line-item snapshots.
        foreach (var item in items)
        {
            item.OrderId = order.Id;
        }

        _db.OrderItems.AddRange(items);

        // 3. Write the payment ledger record.
        payment.OrderId = order.Id;
        _db.Payments.Add(payment);

        await _db.SaveChangesAsync(ct);

        // 4. Hitchhiker lazy eviction: sweep unrelated expired pending orders.
        await ExecuteLazyEvictionAsync(ct);

        await transaction.CommitAsync(ct);

        return SettlementResult.Success;
    }

    public async Task<StatusUpdateResult> UpdateStatusAsync(int id, string currentStatus, string newStatus, CancellationToken ct)
    {
        var affected = await _db.Orders
            .Where(o => o.Id == id && o.Status == currentStatus)
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.Status, newStatus), ct);

        return affected > 0 ? StatusUpdateResult.Success : StatusUpdateResult.Conflict;
    }

    public async Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        return await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task<Order> CreateCheckoutAsync(Order order, CancellationToken ct)
    {
        // Daily order number (SRS §3.1.1): PFY-YYYYMMDD-XXXX where XXXX is today's
        // running count + 1. The unique OrderNumber index is the race backstop — a
        // concurrent checkout that claims the same number violates it (2601/2627),
        // so we clear the tracker and retry with the next number. Workerless.
        //
        // Idempotency: a concurrent request with the SAME IdempotencyKey (double-
        // click, TanStack refetch, network retry) violates the filtered unique
        // IdempotencyKey index instead — we resolve to the winner's row rather than
        // creating a duplicate. Replays are also caught by the service pre-check.
        const int maxAttempts = 5;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            order.OrderNumber = await GenerateOrderNumberAsync(ct);

            _db.Orders.Add(order);

            try
            {
                await _db.SaveChangesAsync(ct);
                return order;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Distinguish an idempotency-key collision from an order-number
                // collision: if a row already exists under this key, another
                // request won the race — return it, never create a duplicate.
                if (order.IdempotencyKey is not null)
                {
                    var existing = await GetByIdempotencyKeyAsync(order.IdempotencyKey, ct);
                    if (existing is not null)
                    {
                        _db.ChangeTracker.Clear();
                        return existing;
                    }
                }

                _db.ChangeTracker.Clear();
            }
        }

        throw new ConflictException("Unable to allocate a unique order number; please retry.");
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"PFY-{datePart}-";

        var count = await _db.Orders
            .AsNoTracking()
            .CountAsync(o => o.OrderNumber.StartsWith(prefix), ct);

        return $"{prefix}{count + 1:D4}";
    }

    /// <summary>Matches SQL Server unique index (2601) / unique constraint (2627) violations.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }

    public async Task<int> ExecuteLazyEvictionAsync(CancellationToken ct)
    {
        var count = await _db.Orders
            .Where(o => o.Status == OrderStatuses.Pending && o.ExpiresAt <= DateTime.UtcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.Status, OrderStatuses.Expired), ct);

        if (count > 0)
        {
            _logger.LogInformation("Lazy eviction expired {Count} pending orders", count);
        }

        return count;
    }
}
