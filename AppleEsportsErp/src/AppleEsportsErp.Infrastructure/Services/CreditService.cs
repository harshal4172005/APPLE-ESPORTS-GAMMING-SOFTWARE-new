using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Credits;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class CreditService : ICreditService
{
    private readonly IUnitOfWork _unitOfWork;

    public CreditService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedResult<CreditDto>> GetCreditsAsync(Guid branchId, string status = "pending", int page = 1, int pageSize = 50)
    {
        var query = _unitOfWork.Repository<CustomerCredit>().Query()
            .Where(c => c.BranchId == branchId);

        if (!string.IsNullOrEmpty(status) && status.ToLower() != "all")
        {
            query = query.Where(c => c.Status.ToLower() == status.ToLower());
        }

        query = query.OrderByDescending(c => c.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var dtos = items.Select(c => new CreditDto
        {
            Id = c.Id,
            BranchId = c.BranchId,
            OperatorId = c.OperatorId,
            BillId = c.BillId,
            CustomerName = c.CustomerName,
            CustomerPhone = c.CustomerPhone,
            PcNumber = c.PcNumber,
            OriginalBillAmount = c.OriginalBillAmount,
            AmountPaidInitially = c.AmountPaidInitially,
            CreditAmount = c.CreditAmount,
            Status = c.Status,
            CreatedAt = c.CreatedAt,
            ClearedAt = c.ClearedAt
        }).ToList();

        return new PaginatedResult<CreditDto>(dtos, total, page, pageSize);
    }

    public async Task<CreditDto> ClearCreditAsync(Guid branchId, Guid operatorId, Guid shiftId, Guid creditId, ClearCreditDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var credit = await _unitOfWork.Repository<CustomerCredit>().Query()
                .FirstOrDefaultAsync(c => c.Id == creditId && c.BranchId == branchId)
                ?? throw new NotFoundException("Credit record not found.");

            if (credit.Status == "cleared")
                throw new AppException("Credit is already cleared.");

            decimal totalPayment = dto.CashAmount + dto.OnlineAmount;
            if (totalPayment != credit.CreditAmount)
                throw new AppException($"Payment mismatch. Expected: {credit.CreditAmount}, Provided: {totalPayment}");

            decimal changeReturned = 0;
            if (dto.CashAmount > 0)
            {
                if (dto.CashReceived < dto.CashAmount)
                    throw new AppException("Cash received is less than cash amount.");
                changeReturned = dto.CashReceived - dto.CashAmount;
            }

            // Create Payment record for the cleared amount
            var payment = new Payment
            {
                BillId = credit.BillId,
                BranchId = branchId,
                OperatorId = operatorId,
                PaymentType = dto.PaymentType,
                TotalAmount = totalPayment,
                CashAmount = dto.CashAmount,
                OnlineAmount = dto.OnlineAmount,
                WalletAmount = 0,
                CashReceived = dto.CashReceived,
                ChangeReturned = changeReturned,
                ActualCashCollected = dto.CashAmount,
                // Assign portions as 0 or calculate if needed, for simplicity we leave them as 0 since the original bill holds the breakdown
                GamingPortion = 0,
                FoodPortion = 0,
                Status = "completed",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _unitOfWork.Repository<Payment>().AddAsync(payment);

            // Update Cash Register
            if (dto.CashAmount > 0)
            {
                var activeRegister = await _unitOfWork.Repository<CashRegister>().Query()
                    .FirstOrDefaultAsync(cr => cr.BranchId == branchId && cr.ShiftId == shiftId && cr.Status == CashRegisterStatus.Open)
                    ?? throw new AppException("No active cash register found for this shift.");

                activeRegister.ExpectedDrawerCash += dto.CashAmount;
                activeRegister.TotalCashSales += dto.CashAmount; // Count as sale on the day it's collected
                _unitOfWork.Repository<CashRegister>().Update(activeRegister);

                var cashTx = new CashTransaction
                {
                    CashRegisterId = activeRegister.Id,
                    BillId = credit.BillId, // Optional, links to original bill
                    BranchId = branchId,
                    OperatorId = operatorId,
                    PcNumber = credit.PcNumber,
                    TransactionType = "credit_clear",
                    CashAmount = dto.CashAmount,
                    GamingAmount = dto.CashAmount, // Assign entirely to gaming for simplicity or prorate if necessary
                    FoodAmount = 0,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _unitOfWork.Repository<CashTransaction>().AddAsync(cashTx);
            }

            // Mark Credit as Cleared
            credit.Status = "cleared";
            credit.ClearedAt = DateTimeOffset.UtcNow;
            credit.ClearedByOperatorId = operatorId;
            _unitOfWork.Repository<CustomerCredit>().Update(credit);

            await _unitOfWork.CommitTransactionAsync();

            return new CreditDto
            {
                Id = credit.Id,
                BranchId = credit.BranchId,
                OperatorId = credit.OperatorId,
                BillId = credit.BillId,
                CustomerName = credit.CustomerName,
                PcNumber = credit.PcNumber,
                OriginalBillAmount = credit.OriginalBillAmount,
                AmountPaidInitially = credit.AmountPaidInitially,
                CreditAmount = credit.CreditAmount,
                Status = credit.Status,
                CreatedAt = credit.CreatedAt,
                ClearedAt = credit.ClearedAt
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}
