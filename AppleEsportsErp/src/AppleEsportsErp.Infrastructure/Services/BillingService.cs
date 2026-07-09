using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Billing;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class BillingService : IBillingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IHubNotificationService _hubNotification;
    private readonly IWalletService _walletService;

    public BillingService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IHubNotificationService hubNotification,
        IWalletService walletService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _hubNotification = hubNotification;
        _walletService = walletService;
    }

    public async Task<PaginatedResult<BillDto>> GetActiveBillsAsync(Guid branchId, int page = 1, int pageSize = 50)
    {
        var query = _unitOfWork.Repository<Bill>().Query()
            .Include(b => b.Items)
            .Include(b => b.Payments)
            .Include(b => b.Pc)
            .Where(b => b.BranchId == branchId && b.Status != BillStatus.Completed)
            .OrderByDescending(b => b.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var dtos = items.Select(MapToDto).ToList();
        return new PaginatedResult<BillDto>(dtos, total, page, pageSize);
    }

    public async Task<List<BillDto>> GetDeferredBillsAsync(Guid branchId)
    {
        var bills = await _unitOfWork.Repository<Bill>().Query()
            .Include(b => b.Items)
            .Include(b => b.Payments)
            .Include(b => b.Pc)
            .Include(b => b.Session)
            .Where(b => b.BranchId == branchId && b.IsDeferred && b.Status == BillStatus.Pending)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return bills.Select(b => MapToDto(b)).ToList();
    }

    public async Task<BillDto> GetBillAsync(Guid branchId, Guid id)
    {
        var bill = await _unitOfWork.Repository<Bill>().Query()
            .Include(b => b.Items)
            .Include(b => b.Payments)
            .Include(b => b.Pc)
            .FirstOrDefaultAsync(b => b.Id == id && b.BranchId == branchId)
            ?? throw new NotFoundException("Bill not found.");

        return MapToDto(bill);
    }

    public async Task<BillDto> ApplyDiscountAsync(Guid branchId, Guid superAdminId, Guid id, ApplyDiscountDto dto)
    {
        var bill = await _unitOfWork.Repository<Bill>().Query()
            .Include(b => b.Items)
            .Include(b => b.Payments)
            .Include(b => b.Pc)
            .FirstOrDefaultAsync(b => b.Id == id && b.BranchId == branchId)
            ?? throw new NotFoundException("Bill not found.");

        if (bill.Status == BillStatus.Completed)
            throw new AppException("Cannot apply discount to a completed bill.");

        decimal discountAmount = 0;
        if (dto.DiscountType == DiscountType.Percentage)
        {
            discountAmount = bill.Subtotal * (dto.DiscountValue / 100);
        }
        else if (dto.DiscountType == DiscountType.Flat)
        {
            discountAmount = dto.DiscountValue;
        }

        if (discountAmount > bill.Subtotal)
            throw new AppException("Discount amount cannot exceed bill subtotal.");

        bill.DiscountType = dto.DiscountType;
        bill.DiscountValue = dto.DiscountValue;
        bill.DiscountAmount = discountAmount;
        bill.DiscountBy = superAdminId;
        bill.DiscountReason = dto.Reason;
        bill.TotalAmount = bill.Subtotal - discountAmount;
        bill.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<Bill>().Update(bill);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = superAdminId,
            UserRole = Roles.SuperAdmin, // Must be SuperAdmin
            UserName = "System",
            Action = AuditActions.DiscountApply,
            BranchId = branchId,
            TargetType = "bill",
            TargetId = bill.Id,
            Details = new { DiscountType = dto.DiscountType.ToString(), Value = dto.DiscountValue, Reason = dto.Reason }
        });

        await _unitOfWork.CommitTransactionAsync();
        await _hubNotification.BroadcastBillingUpdateAsync(branchId, bill.Id);

        return MapToDto(bill);
    }

    public async Task<BillDto> ProcessPaymentAsync(Guid branchId, Guid operatorId, Guid shiftId, Guid id, ProcessPaymentDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var bill = await _unitOfWork.Repository<Bill>().Query()
                .Include(b => b.Items)
                .Include(b => b.Payments)
                .Include(b => b.Pc)
                .Include(b => b.Session)
                .Include(b => b.Member)
                .FirstOrDefaultAsync(b => b.Id == id && b.BranchId == branchId)
                ?? throw new NotFoundException("Bill not found.");

            if (bill.Status == BillStatus.Completed)
                throw new AppException("Bill is already completed.");

            // Link member if passed in payment dto and not already linked
            if (dto.MemberId.HasValue && bill.MemberId == null)
            {
                bill.MemberId = dto.MemberId.Value;
                if (bill.SessionId.HasValue)
                {
                    var session = await _unitOfWork.Repository<Session>().GetByIdAsync(bill.SessionId.Value);
                    if (session != null)
                    {
                        session.MemberId = dto.MemberId.Value;
                        _unitOfWork.Repository<Session>().Update(session);
                    }
                }
            }

            // Calculate total paid vs expected
            decimal totalPayment = dto.CashAmount + dto.OnlineAmount + dto.WalletAmount;
            if (totalPayment + dto.CreditAmount != bill.TotalAmount)
                throw new AppException($"Payment amount mismatch. Expected: {bill.TotalAmount}, Provided: {totalPayment} + Credit: {dto.CreditAmount}");

            decimal changeReturned = 0;
            if (dto.CashAmount > 0)
            {
                if (dto.CashReceived < dto.CashAmount)
                    throw new AppException("Cash received is less than cash amount to be paid.");
                changeReturned = dto.CashReceived - dto.CashAmount;
            }

            // Perform Wallet Deduction first if Wallet payment is involved
            if (dto.WalletAmount > 0)
            {
                if (bill.MemberId == null)
                    throw new AppException("Cannot pay via Wallet for a walk-in customer. Member registration required.");
                    
                decimal totalBill = bill.Subtotal > 0 ? bill.Subtotal : 1;
                decimal gamingDeduction = dto.WalletAmount * (bill.GamingAmount / totalBill);
                decimal foodDeduction = dto.WalletAmount * (bill.FoodAmount / totalBill);

                if (gamingDeduction > 0)
                {
                    await _walletService.DeductWalletAsync(branchId, operatorId, bill.MemberId.Value, new Application.DTOs.Wallets.DeductWalletDto
                    {
                        TargetWallet = WalletType.Gaming,
                        Amount = gamingDeduction,
                        Reason = $"Gaming Payment for Bill {bill.BillNumber}",
                        BillId = bill.Id
                    });
                }
                
                if (foodDeduction > 0)
                {
                    await _walletService.DeductWalletAsync(branchId, operatorId, bill.MemberId.Value, new Application.DTOs.Wallets.DeductWalletDto
                    {
                        TargetWallet = WalletType.Food,
                        Amount = foodDeduction,
                        Reason = $"Food Payment for Bill {bill.BillNumber}",
                        BillId = bill.Id
                    });
                }
            }

            // Process Payment Record
            var payment = new Payment
            {
                BillId = bill.Id,
                BranchId = branchId,
                OperatorId = operatorId,
                PaymentType = dto.PaymentType,
                TotalAmount = totalPayment,
                CashAmount = dto.CashAmount,
                OnlineAmount = dto.OnlineAmount,
                WalletAmount = dto.WalletAmount,
                CashReceived = dto.CashReceived,
                ChangeReturned = changeReturned,
                ActualCashCollected = dto.CashAmount, // Only the portion that counts against the bill
                GamingPortion = bill.GamingAmount - (bill.DiscountAmount * (bill.GamingAmount / (bill.Subtotal > 0 ? bill.Subtotal : 1))), // Prorate discount
                FoodPortion = bill.FoodAmount - (bill.DiscountAmount * (bill.FoodAmount / (bill.Subtotal > 0 ? bill.Subtotal : 1))),
                Status = "completed",
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<Payment>().AddAsync(payment);

            if (dto.CreditAmount > 0)
            {
                var customerCredit = new CustomerCredit
                {
                    BranchId = branchId,
                    OperatorId = operatorId,
                    BillId = bill.Id,
                    CustomerName = !string.IsNullOrWhiteSpace(dto.CustomerName) ? dto.CustomerName : (bill.CustomerName ?? bill.Member?.Username ?? "Walk-in"),
                    CustomerPhone = dto.CustomerPhone ?? "N/A",
                    PcNumber = bill.Pc?.PcNumber ?? "N/A",
                    OriginalBillAmount = bill.TotalAmount,
                    AmountPaidInitially = totalPayment,
                    CreditAmount = dto.CreditAmount,
                    Status = "pending",
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _unitOfWork.Repository<CustomerCredit>().AddAsync(customerCredit);
            }

            // Update Bill
            bill.PaymentType = dto.PaymentType;
            bill.CashAmount = dto.CashAmount;
            bill.OnlineAmount = dto.OnlineAmount;
            bill.WalletAmount = dto.WalletAmount;
            bill.CashReceived = dto.CashReceived;
            bill.ChangeReturned = changeReturned;
            bill.ActualCashCollected = dto.CashAmount;
            bill.Status = BillStatus.Completed;
            bill.CompletedAt = DateTimeOffset.UtcNow;
            bill.IsDeferred = false;
            bill.UpdatedAt = DateTimeOffset.UtcNow;
            
            _unitOfWork.Repository<Bill>().Update(bill);

            // Cash Register Tracking (SOP §10.2)
            if (dto.CashAmount > 0)
            {
                var activeRegister = await _unitOfWork.Repository<CashRegister>().Query()
                    .FirstOrDefaultAsync(cr => cr.BranchId == branchId && cr.ShiftId == shiftId && cr.Status == CashRegisterStatus.Open)
                    ?? throw new AppException("No active cash register found for this shift.");

                activeRegister.ExpectedDrawerCash += dto.CashAmount;
                activeRegister.TotalCashSales += dto.CashAmount;
                _unitOfWork.Repository<CashRegister>().Update(activeRegister);

                var cashTx = new CashTransaction
                {
                    CashRegisterId = activeRegister.Id,
                    BillId = bill.Id,
                    BranchId = branchId,
                    OperatorId = operatorId,
                    PcNumber = bill.Pc?.PcNumber,
                    TransactionType = "billing",
                    CashAmount = dto.CashAmount,
                    GamingAmount = payment.GamingPortion * (dto.CashAmount / totalPayment), // Prorate cash to gaming
                    FoodAmount = payment.FoodPortion * (dto.CashAmount / totalPayment),     // Prorate cash to food
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _unitOfWork.Repository<CashTransaction>().AddAsync(cashTx);
            }

            Guid? completedSessionId = null;
            Guid? releasedPcId = null;

            // Release PC & Session (SOP §9.2)
            if (bill.Pc != null)
            {
                var pc = bill.Pc;
                
                // If there's an active session, stop it automatically upon payment
                if (bill.SessionId.HasValue)
                {
                    var session = await _unitOfWork.Repository<Session>().GetByIdAsync(bill.SessionId.Value);
                    if (session != null && session.State == SessionState.Active)
                    {
                        var now = DateTimeOffset.UtcNow;
                        session.State = SessionState.Completed;
                        session.UpdatedAt = now;
                        session.EndTime = now;
                        session.ActualDurationMin = (int)(now - session.StartTime).TotalMinutes;
                        _unitOfWork.Repository<Session>().Update(session);
                        completedSessionId = session.Id;
                    }
                }

                // If PC is AwaitingBilling or Active, we release it back to Idle
                if (pc.State == PcState.AwaitingBilling || pc.State == PcState.Active)
                {
                    pc.State = PcState.Idle;
                    pc.CurrentSessionId = null;
                    _unitOfWork.Repository<Pc>().Update(pc);
                    releasedPcId = pc.Id;
                }
            }

            // Log Audit
            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "Operator",
                UserName = "System",
                Action = AuditActions.PaymentProcess,
                BranchId = branchId,
                TargetType = "bill",
                TargetId = bill.Id,
                Details = new { PaymentType = dto.PaymentType.ToString(), Total = totalPayment, Cash = dto.CashAmount }
            });

            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "Operator",
                UserName = "System",
                Action = AuditActions.BillComplete,
                BranchId = branchId,
                TargetType = "bill",
                TargetId = bill.Id,
                Details = new { BillNumber = bill.BillNumber }
            });

            await _unitOfWork.CommitTransactionAsync();
            await _hubNotification.BroadcastBillingUpdateAsync(branchId, bill.Id);
            
            if (completedSessionId.HasValue)
                await _hubNotification.BroadcastSessionUpdateAsync(branchId, completedSessionId.Value);
            
            if (releasedPcId.HasValue)
                await _hubNotification.BroadcastPcStatusChangeAsync(branchId, releasedPcId.Value);

            return MapToDto(bill);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<BillDto> RemoveBillItemAsync(Guid branchId, Guid operatorId, Guid billId, Guid billItemId)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var bill = await _unitOfWork.Repository<Bill>().Query()
                .Include(b => b.Items)
                .Include(b => b.Payments)
                .Include(b => b.Pc)
                .FirstOrDefaultAsync(b => b.Id == billId && b.BranchId == branchId)
                ?? throw new NotFoundException("Bill not found.");

            if (bill.Status == BillStatus.Completed)
                throw new AppException("Cannot modify a completed bill.");

            var itemToRemove = bill.Items.FirstOrDefault(i => i.Id == billItemId)
                ?? throw new NotFoundException("Bill item not found.");

            if (itemToRemove.ItemType.ToLower() == "gaming")
                throw new AppException("Cannot manually remove gaming items.");

            // Restore Inventory if applicable
            if (itemToRemove.InventoryId.HasValue)
            {
                var inventoryItem = await _unitOfWork.Repository<InventoryItem>().GetByIdAsync(itemToRemove.InventoryId.Value);
                if (inventoryItem != null)
                {
                    inventoryItem.CurrentStock += itemToRemove.Quantity;
                    inventoryItem.SoldQty -= itemToRemove.Quantity;
                    inventoryItem.UpdatedAt = DateTimeOffset.UtcNow;
                    _unitOfWork.Repository<InventoryItem>().Update(inventoryItem);

                    var log = new InventoryLog
                    {
                        InventoryId = inventoryItem.Id,
                        OperatorId = operatorId,
                        BranchId = branchId,
                        Action = "void_return",
                        Quantity = itemToRemove.Quantity,
                        Reason = "Item removed from bill",
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    await _unitOfWork.Repository<InventoryLog>().AddAsync(log);
                }
            }

            // Adjust Bill Totals
            bill.FoodAmount -= itemToRemove.TotalPrice;
            if (bill.FoodAmount < 0) bill.FoodAmount = 0;
            
            bill.Subtotal -= itemToRemove.TotalPrice;
            if (bill.Subtotal < 0) bill.Subtotal = 0;

            // Recalculate discount if percentage based
            if (bill.DiscountType == DiscountType.Percentage && bill.Subtotal > 0)
            {
                bill.DiscountAmount = bill.Subtotal * (bill.DiscountValue / 100);
            }
            else if (bill.DiscountType == DiscountType.Flat)
            {
                // Ensure flat discount doesn't exceed new subtotal
                if (bill.DiscountAmount > bill.Subtotal)
                    bill.DiscountAmount = bill.Subtotal;
            }

            bill.TotalAmount = bill.Subtotal - bill.DiscountAmount;
            if (bill.TotalAmount < 0) bill.TotalAmount = 0;
            
            bill.UpdatedAt = DateTimeOffset.UtcNow;

            bill.Items.Remove(itemToRemove);
            _unitOfWork.Repository<BillItem>().Remove(itemToRemove);
            _unitOfWork.Repository<Bill>().Update(bill);

            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "Operator",
                UserName = "System",
                Action = "bill_item_removed",
                BranchId = branchId,
                TargetType = "bill",
                TargetId = bill.Id,
                Details = new { ItemName = itemToRemove.ItemName, Quantity = itemToRemove.Quantity, Amount = itemToRemove.TotalPrice }
            });

            await _unitOfWork.CommitTransactionAsync();
            await _hubNotification.BroadcastBillingUpdateAsync(branchId, bill.Id);

            return MapToDto(bill);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private static BillDto MapToDto(Bill b)
    {
        return new BillDto
        {
            Id = b.Id,
            BillNumber = b.BillNumber,
            SessionId = b.SessionId,
            PcId = b.PcId,
            PcNumber = b.Pc?.PcNumber,
            BranchId = b.BranchId,
            OperatorId = b.OperatorId,
            ShiftId = b.ShiftId,
            CustomerName = b.CustomerName,
            MemberId = b.MemberId,
            GamingAmount = b.GamingAmount,
            FoodAmount = b.FoodAmount,
            Subtotal = b.Subtotal,
            DiscountType = b.DiscountType,
            DiscountValue = b.DiscountValue,
            DiscountAmount = b.DiscountAmount,
            DiscountReason = b.DiscountReason,
            TotalAmount = b.TotalAmount,
            Status = b.Status,
            IsDeferred = b.IsDeferred,
            CreatedAt = b.CreatedAt,
            SessionEndTime = b.Session?.EndTime,
            Items = b.Items?.Select(i => new BillItemDto
            {
                Id = i.Id,
                ItemType = i.ItemType,
                ItemName = i.ItemName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }).ToList() ?? new List<BillItemDto>(),
            Payments = b.Payments?.Select(p => new PaymentDto
            {
                Id = p.Id,
                PaymentType = p.PaymentType,
                TotalAmount = p.TotalAmount,
                CashAmount = p.CashAmount,
                OnlineAmount = p.OnlineAmount,
                WalletAmount = p.WalletAmount,
                CashReceived = p.CashReceived,
                ChangeReturned = p.ChangeReturned,
                ActualCashCollected = p.ActualCashCollected,
                CreatedAt = p.CreatedAt
            }).ToList() ?? new List<PaymentDto>()
        };
    }
}
