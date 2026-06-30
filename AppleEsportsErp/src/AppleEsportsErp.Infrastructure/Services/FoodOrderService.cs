using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.FoodOrders;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class FoodOrderService : IFoodOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IHubNotificationService _hubNotification;

    public FoodOrderService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IHubNotificationService hubNotification)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _hubNotification = hubNotification;
    }

    public async Task<PaginatedResult<FoodOrderDto>> GetActiveOrdersAsync(Guid branchId, int page = 1, int pageSize = 50)
    {
        var query = _unitOfWork.Repository<FoodOrder>().Query()
            .Include(o => o.Items)
            .Include(o => o.Pc)
            .Where(o => o.BranchId == branchId && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.OrderTime);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var dtos = items.Select(MapToDto).ToList();
        return new PaginatedResult<FoodOrderDto>(dtos, total, page, pageSize);
    }

    public async Task<FoodOrderDto> GetOrderAsync(Guid branchId, Guid id)
    {
        var order = await _unitOfWork.Repository<FoodOrder>().Query()
            .Include(o => o.Items)
            .Include(o => o.Pc)
            .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId)
            ?? throw new NotFoundException("Order not found.");

        return MapToDto(order);
    }

    public async Task<FoodOrderDto> PlaceOrderAsync(Guid branchId, Guid operatorId, Guid shiftId, CreateFoodOrderDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            
            // Generate order number
            var count = await _unitOfWork.Repository<FoodOrder>().Query().CountAsync(o => o.OrderTime >= new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero));
            var orderNum = $"ORD-{now:yyMMdd}-{count + 1:D4}";

            var order = new FoodOrder
            {
                OrderNumber = orderNum,
                SessionId = dto.SessionId,
                PcId = dto.PcId,
                BranchId = branchId,
                OperatorId = operatorId,
                CustomerName = dto.CustomerName,
                OrderTime = now,
                Status = OrderStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };

            decimal totalAmount = 0;

            foreach (var itemDto in dto.Items)
            {
                var inventoryItem = await _unitOfWork.Repository<InventoryItem>().GetByIdAsync(itemDto.InventoryId)
                    ?? throw new NotFoundException($"Inventory item {itemDto.InventoryId} not found.");

                if (inventoryItem.CurrentStock < itemDto.Quantity)
                    throw new AppException($"Insufficient stock for {inventoryItem.ItemName}. Available: {inventoryItem.CurrentStock}");

                // Add order item
                var totalPrice = inventoryItem.Price * itemDto.Quantity;
                totalAmount += totalPrice;
                
                order.Items.Add(new FoodOrderItem
                {
                    InventoryId = inventoryItem.Id,
                    ItemName = inventoryItem.ItemName,
                    Quantity = itemDto.Quantity,
                    UnitPrice = inventoryItem.Price,
                    TotalPrice = totalPrice,
                    CreatedAt = now
                });
            }

            order.TotalAmount = totalAmount;

            // If linked to a Session, lookup active bill to verify it exists and set order attributes
            Bill? activeBill = null;
            if (dto.SessionId.HasValue)
            {
                activeBill = await _unitOfWork.Repository<Bill>().Query()
                    .FirstOrDefaultAsync(b => b.SessionId == dto.SessionId && b.Status != BillStatus.Completed);

                if (activeBill == null)
                    throw new AppException("Cannot place order. No active bill found for the session.");

                order.PaymentType = "session_bill";
                order.MemberId = activeBill.MemberId;
            }

            await _unitOfWork.Repository<FoodOrder>().AddAsync(order);

            // Audit
            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "Operator",
                UserName = "System",
                Action = "food_order_create",
                BranchId = branchId,
                TargetType = "food_order",
                TargetId = order.Id,
                Details = new { OrderNumber = orderNum, Total = totalAmount, ItemCount = order.Items.Count }
            });

            await _unitOfWork.CommitTransactionAsync();
            
            await _hubNotification.BroadcastFoodOrderUpdateAsync(branchId, order.Id);
            if (activeBill != null)
            {
                await _hubNotification.BroadcastBillingUpdateAsync(branchId, activeBill.Id);
            }

            return MapToDto(order);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<FoodOrderDto> UpdateOrderStatusAsync(Guid branchId, Guid operatorId, Guid id, UpdateOrderStatusDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var order = await _unitOfWork.Repository<FoodOrder>().Query()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId)
                ?? throw new NotFoundException("Order not found.");

            if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Delivered)
                throw new AppException($"Order is already {order.Status} and cannot be modified.");

            var now = DateTimeOffset.UtcNow;
            var oldStatus = order.Status;
            order.Status = dto.Status;
            order.UpdatedAt = now;

            if (dto.Status == OrderStatus.Preparing)
                order.AcceptedAt = now;
            else if (dto.Status == OrderStatus.Ready)
                order.ReadyAt = now;
            else if (dto.Status == OrderStatus.Delivered)
            {
                order.DeliveredAt = now;

                // 1. Deduct stock and increment SoldQty for each item in the order
                foreach (var item in order.Items)
                {
                    var inventoryItem = await _unitOfWork.Repository<InventoryItem>().GetByIdAsync(item.InventoryId);
                    if (inventoryItem != null)
                    {
                        if (inventoryItem.CurrentStock < item.Quantity)
                            throw new AppException($"Insufficient stock for {inventoryItem.ItemName}. Available: {inventoryItem.CurrentStock}");

                        int oldStock = inventoryItem.CurrentStock;
                        inventoryItem.CurrentStock -= item.Quantity;
                        inventoryItem.SoldQty += item.Quantity;
                        inventoryItem.UpdatedAt = now;
                        _unitOfWork.Repository<InventoryItem>().Update(inventoryItem);

                        // Log the inventory deduction (Action = "sale")
                        var log = new InventoryLog
                        {
                            InventoryId = inventoryItem.Id,
                            BranchId = branchId,
                            OperatorId = operatorId,
                            Action = "sale",
                            Quantity = item.Quantity,
                            OldValue = oldStock.ToString(),
                            NewValue = inventoryItem.CurrentStock.ToString(),
                            Reason = $"Delivered order {order.OrderNumber}",
                            CreatedAt = now
                        };
                        await _unitOfWork.Repository<InventoryLog>().AddAsync(log);
                    }
                }

                // 2. Automatically append to active bill if linked to a session
                if (order.SessionId.HasValue)
                {
                    var activeBill = await _unitOfWork.Repository<Bill>().Query()
                        .Include(b => b.Items)
                        .FirstOrDefaultAsync(b => b.SessionId == order.SessionId.Value && b.Status != BillStatus.Completed);

                    if (activeBill == null)
                        throw new AppException("Cannot deliver order. No active bill found for the session.");

                    foreach (var item in order.Items)
                    {
                        activeBill.Items.Add(new BillItem
                        {
                            ItemType = "food",
                            ItemName = item.ItemName,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            TotalPrice = item.TotalPrice,
                            InventoryId = item.InventoryId,
                            CreatedAt = now
                        });
                    }

                    activeBill.FoodAmount += order.TotalAmount;
                    activeBill.Subtotal += order.TotalAmount;
                    activeBill.TotalAmount += order.TotalAmount;
                    activeBill.UpdatedAt = now;
                    _unitOfWork.Repository<Bill>().Update(activeBill);

                    await _hubNotification.BroadcastBillingUpdateAsync(branchId, activeBill.Id);
                }
            }
            else if (dto.Status == OrderStatus.Completed)
                order.CompletedAt = now;
            else if (dto.Status == OrderStatus.Cancelled)
            {
                order.CancelledReason = dto.Reason;
            }

            _unitOfWork.Repository<FoodOrder>().Update(order);

            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "Operator",
                UserName = "System",
                Action = "food_order_update",
                BranchId = branchId,
                TargetType = "food_order",
                TargetId = order.Id,
                Details = new { Status = dto.Status.ToString(), Reason = dto.Reason }
            });

            await _unitOfWork.CommitTransactionAsync();
            await _hubNotification.BroadcastFoodOrderUpdateAsync(branchId, order.Id);

            return MapToDto(order);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private static FoodOrderDto MapToDto(FoodOrder o)
    {
        return new FoodOrderDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            SessionId = o.SessionId,
            PcId = o.PcId,
            PcNumber = o.Pc?.PcNumber,
            BranchId = o.BranchId,
            OperatorId = o.OperatorId,
            CustomerName = o.CustomerName,
            TotalAmount = o.TotalAmount,
            PaymentType = o.PaymentType,
            Status = o.Status,
            CancelledReason = o.CancelledReason,
            OrderTime = o.OrderTime,
            DeliveredAt = o.DeliveredAt,
            Items = o.Items?.Select(i => new FoodOrderItemDto
            {
                Id = i.Id,
                InventoryId = i.InventoryId,
                ItemName = i.ItemName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }).ToList() ?? new List<FoodOrderItemDto>()
        };
    }
}
