using FluentValidation;
using FluentValidation.Results;
using PollenForYouApi.DTOs;
using PollenForYouApi.Entities;
using PollenForYouApi.Exceptions;
using PollenForYouApi.Repositories;

namespace PollenForYouApi.Services;

/// <summary>
/// Order lifecycle orchestration (SRS §3.1): FIFO queue reads, RowVersion-guarded
/// workspace claims (loser → <c>409</c>), atomic settlement that recomputes totals
/// from DB base prices and freezes snapshots, and forward-only status transitions.
/// </summary>
public class OrderService : IOrderService
{
    private const int MaxPageSize = 50;

    // Forward-only fulfillment graph (SRS §3.1). Pending → In Production is
    // handled exclusively by settlement; Pending → Expired by lazy eviction.
    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedTransitions =
        new Dictionary<string, HashSet<string>>
        {
            [OrderStatuses.Pending] = [OrderStatuses.Cancelled],
            [OrderStatuses.InProduction] = [OrderStatuses.ReadyForDispatch, OrderStatuses.Cancelled],
            [OrderStatuses.ReadyForDispatch] = [OrderStatuses.Dispatched, OrderStatuses.Cancelled],
            [OrderStatuses.Dispatched] = [OrderStatuses.Completed, OrderStatuses.Cancelled]
        };

    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<PagedResult<OrderQueueDto>> GetQueueAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        return await _orderRepository.GetQueuePageAsync(page, pageSize, ct);
    }

    public async Task<OrderDetailDto> ClaimOrderAsync(string orderNumber, int adminUserId, CancellationToken ct)
    {
        var result = await _orderRepository.ClaimAsync(orderNumber, adminUserId, ct);

        if (result == ClaimResult.Success)
        {
            _logger.LogInformation("Admin {AdminUserId} claimed order {OrderNumber}", adminUserId, orderNumber);
        }
        else
        {
            _logger.LogWarning(
                "Admin {AdminUserId} could not claim order {OrderNumber} ({Result})", adminUserId, orderNumber, result);
        }

        return result switch
        {
            ClaimResult.Success => await GetDetailOrThrowAsync(orderNumber, ct),
            ClaimResult.NotFound => throw new NotFoundException($"Order with number {orderNumber} was not found."),
            ClaimResult.NotClaimable => throw new ConflictException(
                "Order is not claimable: it is not pending or has expired."),
            _ => throw new ConflictException("Order is already claimed by another admin.")
        };
    }

    public async Task ReleaseClaimAsync(string orderNumber, int adminUserId, CancellationToken ct)
    {
        if (!await _orderRepository.ReleaseClaimAsync(orderNumber, adminUserId, ct))
        {
            throw new NotFoundException($"Order with number {orderNumber} was not found or is not claimed by you.");
        }

        _logger.LogInformation("Admin {AdminUserId} released order {OrderNumber}", adminUserId, orderNumber);
    }

    public async Task<OrderDetailDto> ConfirmSettlementAsync(OrderConfirmationRequestDto dto, int adminUserId, CancellationToken ct)
    {
        // 1. Server-side ground truth: load the active products being confirmed.
        //    Client-submitted pricing is discarded (AGENT.md §14).
        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToArray();
        var products = await _productRepository.GetByIdsAsync(productIds, ct);

        var missing = productIds.Except(products.Select(p => p.Id)).ToArray();
        if (missing.Length > 0)
        {
            throw new ValidationException([
                new ValidationFailure(nameof(OrderConfirmationRequestDto.Items),
                    $"The following products are not available: {string.Join(", ", missing)}.")
            ]);
        }

        var priceByProduct = products.ToDictionary(p => p.Id);

        // 2. Build frozen snapshots and recompute the total from DB base prices.
        var items = dto.Items.Select(i => new OrderItem
        {
            ProductId = i.ProductId,
            ProductNameSnapshot = priceByProduct[i.ProductId].Name,
            PriceAtPurchase = priceByProduct[i.ProductId].BasePrice,
            Quantity = i.Quantity
        }).ToList();

        var totalPrice = items.Sum(i => i.PriceAtPurchase * i.Quantity);

        var payment = new Payment
        {
            PaymentStage = dto.Payment!.PaymentStage,
            PaymentMethod = dto.Payment!.PaymentMethod,
            AmountPaid = dto.Payment!.AmountPaid,
            TransactionReference = dto.Payment!.TransactionReference,
            VerifiedByAdminId = adminUserId
        };

        _logger.LogInformation(
            "Admin {AdminUserId} settling order {OrderNumber}: {ItemCount} items, total {TotalPrice}",
            adminUserId, dto.OrderNumber, items.Count, totalPrice);

        // 3. Atomic settlement (promote → frozen items + payment → hitchhiker eviction).
        var result = await _orderRepository.SettleAsync(dto.OrderNumber, adminUserId, items, totalPrice, payment, ct);

        return result switch
        {
            SettlementResult.Success => await GetDetailOrThrowAsync(dto.OrderNumber, ct),
            SettlementResult.NotFound => throw new NotFoundException($"Order with number {dto.OrderNumber} was not found."),
            _ => throw new ConflictException(
                "Order could not be settled: it is not pending, has expired, or is not claimed by you.")
        };
    }

    public async Task<OrderDetailDto> UpdateStatusAsync(int id, OrderStatusUpdateRequestDto dto, CancellationToken ct)
    {
        var currentStatus = await _orderRepository.GetStatusByIdAsync(id, ct)
            ?? throw new NotFoundException($"Order with id {id} was not found.");

        if (!IsTransitionAllowed(currentStatus, dto.Status))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(OrderStatusUpdateRequestDto.Status),
                    $"Cannot transition from '{currentStatus}' to '{dto.Status}'.")
            ]);
        }

        var result = await _orderRepository.UpdateStatusAsync(id, currentStatus, dto.Status, ct);
        if (result == StatusUpdateResult.Conflict)
        {
            throw new ConflictException("Order status changed concurrently; refresh and retry.");
        }

        _logger.LogInformation(
            "Order {OrderId} transitioned {FromStatus} → {ToStatus}", id, currentStatus, dto.Status);

        return await _orderRepository.GetDetailByIdAsync(id, ct)
            ?? throw new NotFoundException($"Order with id {id} was not found.");
    }

    private async Task<OrderDetailDto> GetDetailOrThrowAsync(string orderNumber, CancellationToken ct)
    {
        return await _orderRepository.GetDetailByOrderNumberAsync(orderNumber, ct)
            ?? throw new NotFoundException($"Order with number {orderNumber} was not found.");
    }

    private static bool IsTransitionAllowed(string currentStatus, string targetStatus)
    {
        return AllowedTransitions.TryGetValue(currentStatus, out var targets) && targets.Contains(targetStatus);
    }
}
