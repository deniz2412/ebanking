using EBanking.TransferService.Data;
using EBanking.TransferService.Models;
using EBanking.TransferService.Models.DTOs;
using EBanking.TransferService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EBanking.TransferService.Services;

public class TransferService : ITransferService
{
    private readonly TransferDbContext _context;
    private readonly IEventPublisher _eventPublisher;
    private readonly IValidationService _validationService;
    private readonly ILogger<TransferService> _logger;

    public TransferService(
        TransferDbContext context,
        IEventPublisher eventPublisher,
        IValidationService validationService,
        ILogger<TransferService> logger)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<TransferResponse> CreateTransferAsync(string userId, CreateTransferRequest request, string idempotencyKey)
    {
        // Validate account ownership
        if (!await _validationService.ValidateAccountOwnershipAsync(userId, request.FromAccountNumber))
        {
            throw new ArgumentException("Invalid from account number");
        }

        // Validate IBAN for external transfers
        if (request.Type == TransferType.External && !_validationService.ValidateIBAN(request.ToAccountNumber))
        {
            throw new ArgumentException("Invalid IBAN for external transfer");
        }

        // Validate transfer limits
        if (!_validationService.ValidateTransferLimits(request.Amount, request.Type))
        {
            throw new ArgumentException("Transfer amount exceeds allowed limits");
        }

        // Internal transfers settle immediately (no external clearing); external
        // transfers stay Pending until the payment rail confirms them.
        var isInternal = request.Type == TransferType.Internal;
        var now = DateTime.UtcNow;

        var transfer = new Transfer
        {
            UserId = userId,
            FromAccountNumber = request.FromAccountNumber,
            ToAccountNumber = request.ToAccountNumber,
            ToAccountName = request.ToAccountName,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Reference = request.Reference,
            Type = request.Type,
            Status = isInternal ? TransferStatus.Completed : TransferStatus.Pending,
            ProcessedAt = isInternal ? now : null,
            IdempotencyKey = idempotencyKey,
            CorrelationId = Guid.NewGuid().ToString(),
            RequestedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Transfers.Add(transfer);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Publish event after transaction commit. Internal transfers are already
            // settled, so they emit the completion event that Notification and Audit
            // consume; external transfers emit "created" and complete later.
            if (isInternal)
                await _eventPublisher.PublishTransferCompletedAsync(transfer);
            else
                await _eventPublisher.PublishTransferCreatedAsync(transfer);

            _logger.LogInformation("Transfer {Status}: {TransferId} for user {UserId}",
                transfer.Status, transfer.Id, userId);

            return MapToTransferResponse(transfer);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<TransferResponse?> GetTransferAsync(string userId, int transferId)
    {
        var transfer = await _context.Transfers
            .FirstOrDefaultAsync(t => t.Id == transferId && t.UserId == userId);

        return transfer != null ? MapToTransferResponse(transfer) : null;
    }

    public async Task<PagedTransferResponse> GetTransfersAsync(string userId, int page, int pageSize, TransferStatus? status = null, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Transfers.Where(t => t.UserId == userId);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (from.HasValue)
            query = query.Where(t => t.RequestedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.RequestedAt <= to.Value);

        var totalCount = await query.CountAsync();
        var transfers = await query
            .OrderByDescending(t => t.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedTransferResponse
        {
            Transfers = transfers.Select(MapToTransferResponse),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    public async Task<StandingOrderResponse> CreateStandingOrderAsync(string userId, CreateStandingOrderRequest request, string idempotencyKey)
    {
        // Validate account ownership
        if (!await _validationService.ValidateAccountOwnershipAsync(userId, request.FromAccountNumber))
        {
            throw new ArgumentException("Invalid from account number");
        }

        // Calculate next execution date
        var nextExecution = CalculateNextExecutionDate(request.StartDate, request.Frequency);

        var standingOrder = new StandingOrder
        {
            UserId = userId,
            FromAccountNumber = request.FromAccountNumber,
            ToAccountNumber = request.ToAccountNumber,
            ToAccountName = request.ToAccountName,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Reference = request.Reference,
            Frequency = request.Frequency,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NextExecutionDate = nextExecution,
            MaxExecutions = request.MaxExecutions,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.StandingOrders.Add(standingOrder);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Standing order created: {StandingOrderId} for user {UserId}", standingOrder.Id, userId);

            return MapToStandingOrderResponse(standingOrder);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<StandingOrderResponse?> GetStandingOrderAsync(string userId, int standingOrderId)
    {
        var standingOrder = await _context.StandingOrders
            .FirstOrDefaultAsync(so => so.Id == standingOrderId && so.UserId == userId);

        return standingOrder != null ? MapToStandingOrderResponse(standingOrder) : null;
    }

    public async Task<PagedStandingOrderResponse> GetStandingOrdersAsync(string userId, int page, int pageSize, bool? isActive = null)
    {
        var query = _context.StandingOrders.Where(so => so.UserId == userId);

        if (isActive.HasValue)
            query = query.Where(so => so.IsActive == isActive.Value);

        var totalCount = await query.CountAsync();
        var standingOrders = await query
            .OrderByDescending(so => so.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedStandingOrderResponse
        {
            StandingOrders = standingOrders.Select(MapToStandingOrderResponse),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    public async Task<bool> CancelStandingOrderAsync(string userId, int standingOrderId)
    {
        var standingOrder = await _context.StandingOrders
            .FirstOrDefaultAsync(so => so.Id == standingOrderId && so.UserId == userId);

        if (standingOrder == null)
            return false;

        standingOrder.IsActive = false;
        standingOrder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Standing order cancelled: {StandingOrderId} for user {UserId}", standingOrderId, userId);

        return true;
    }

    private static TransferResponse MapToTransferResponse(Transfer transfer)
    {
        return new TransferResponse
        {
            Id = transfer.Id,
            FromAccountNumber = transfer.FromAccountNumber,
            ToAccountNumber = transfer.ToAccountNumber,
            ToAccountName = transfer.ToAccountName,
            Amount = transfer.Amount,
            Currency = transfer.Currency,
            Description = transfer.Description,
            Reference = transfer.Reference,
            Type = transfer.Type,
            Status = transfer.Status,
            IdempotencyKey = transfer.IdempotencyKey,
            RequestedAt = transfer.RequestedAt,
            ProcessedAt = transfer.ProcessedAt,
            ErrorMessage = transfer.ErrorMessage
        };
    }

    private static StandingOrderResponse MapToStandingOrderResponse(StandingOrder standingOrder)
    {
        return new StandingOrderResponse
        {
            Id = standingOrder.Id,
            FromAccountNumber = standingOrder.FromAccountNumber,
            ToAccountNumber = standingOrder.ToAccountNumber,
            ToAccountName = standingOrder.ToAccountName,
            Amount = standingOrder.Amount,
            Currency = standingOrder.Currency,
            Description = standingOrder.Description,
            Reference = standingOrder.Reference,
            Frequency = standingOrder.Frequency,
            StartDate = standingOrder.StartDate,
            EndDate = standingOrder.EndDate,
            NextExecutionDate = standingOrder.NextExecutionDate,
            IsActive = standingOrder.IsActive,
            ExecutionCount = standingOrder.ExecutionCount,
            MaxExecutions = standingOrder.MaxExecutions,
            CreatedAt = standingOrder.CreatedAt
        };
    }

    private static DateTime CalculateNextExecutionDate(DateTime startDate, Frequency frequency)
    {
        return frequency switch
        {
            Frequency.Daily => startDate.AddDays(1),
            Frequency.Weekly => startDate.AddDays(7),
            Frequency.Monthly => startDate.AddMonths(1),
            Frequency.Quarterly => startDate.AddMonths(3),
            Frequency.Yearly => startDate.AddYears(1),
            _ => startDate.AddMonths(1)
        };
    }
}
