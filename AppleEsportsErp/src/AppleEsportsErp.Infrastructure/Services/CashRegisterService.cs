using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Cash;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class CashRegisterService : ICashRegisterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IHubNotificationService _hubNotification;
    private readonly IEmailService _emailService;

    public CashRegisterService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IHubNotificationService hubNotification,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _hubNotification = hubNotification;
        _emailService = emailService;
    }

    public async Task<CashRegisterDto> GetActiveRegisterAsync(Guid branchId, Guid shiftId)
    {
        var register = await _unitOfWork.Repository<CashRegister>().Query()
            .Include(r => r.CashTransactions)
            .FirstOrDefaultAsync(r => r.BranchId == branchId && r.ShiftId == shiftId && r.Status != CashRegisterStatus.Closed)
            ?? throw new NotFoundException("No active cash register found for this shift.");

        return MapToDto(register);
    }

    public async Task<CashRegisterDto> OpenRegisterAsync(Guid branchId, Guid operatorId, Guid shiftId, OpenRegisterDto dto)
    {
        var existing = await _unitOfWork.Repository<CashRegister>().Query()
            .FirstOrDefaultAsync(r => r.BranchId == branchId && r.ShiftId == shiftId && r.Status == CashRegisterStatus.Open);
            
        if (existing != null)
            throw new AppException("Cash register is already open for this shift.");

        var register = new CashRegister
        {
            BranchId = branchId,
            OperatorId = operatorId,
            ShiftId = shiftId,
            OpeningBalance = dto.OpeningBalance,
            ExpectedDrawerCash = dto.OpeningBalance, // Only opening balance affects drawer cash initially
            TotalCashSales = 0,
            TotalSplitCash = 0,
            Status = CashRegisterStatus.Open,
            OpenedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<CashRegister>().AddAsync(register);
        
        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = "cash_register_open",
            BranchId = branchId,
            TargetType = "cash_register",
            TargetId = register.Id,
            Details = new { OpeningBalance = dto.OpeningBalance }
        });

        await _unitOfWork.CommitTransactionAsync();
        await _hubNotification.BroadcastCashRegisterUpdateAsync(branchId, register.Id);

        return MapToDto(register);
    }

    public async Task<CashRegisterDto> AddTransactionAsync(Guid branchId, Guid operatorId, Guid shiftId, AddCashTransactionDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var register = await _unitOfWork.Repository<CashRegister>().Query()
                .Include(r => r.CashTransactions)
                .FirstOrDefaultAsync(r => r.BranchId == branchId && r.ShiftId == shiftId && r.Status == CashRegisterStatus.Open)
                ?? throw new NotFoundException("No active cash register found for this shift.");

            var tx = new CashTransaction
            {
                CashRegisterId = register.Id,
                BranchId = branchId,
                OperatorId = operatorId,
                CashAmount = dto.Amount,
                GamingAmount = 0,
                FoodAmount = 0,
                CustomerName = dto.Reason ?? "Operator Adjustment",
                TransactionType = dto.TransactionType,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<CashTransaction>().AddAsync(tx);

            // Inwards increase drawer cash, Withdrawals/Expenses decrease drawer cash
            register.ExpectedDrawerCash += dto.Amount;
            
            _unitOfWork.Repository<CashRegister>().Update(register);

            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "Operator",
                UserName = "System",
                Action = $"cash_transaction_{dto.TransactionType}",
                BranchId = branchId,
                TargetType = "cash_register",
                TargetId = register.Id,
                Details = new { Amount = dto.Amount, Reason = dto.Reason }
            });

            await _unitOfWork.CommitTransactionAsync();
            await _hubNotification.BroadcastCashRegisterUpdateAsync(branchId, register.Id);

            try 
            {
                // Send Notification Email to Super Admins
                var superAdmins = await _unitOfWork.Repository<Operator>().Query()
                    .Where(o => o.IsGlobalAdmin && o.Status == OperatorStatus.Active)
                    .ToListAsync();
                    
                if (superAdmins.Any())
                {
                    var operatorEntity = await _unitOfWork.Repository<Operator>().GetByIdAsync(operatorId);
                    var branchEntity = await _unitOfWork.Repository<Branch>().GetByIdAsync(branchId);
                    var opName = operatorEntity?.FullName ?? "System";
                    var branchName = branchEntity?.Name ?? "Unknown Branch";
                    
                    // Alpine Linux docker images lack tzdata, so manual UTC+5:30 is foolproof for IST
                    var localTime = DateTime.UtcNow.AddHours(5).AddMinutes(30);
                    
                    var emailSubject = $"🚨 Manual Cash Transaction Alert - {branchName}";
                    var emailBody = $@"
                        <div style='background-color:#050505; color:#ffffff; font-family:""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; padding:40px 20px;'>
                            <h2 style='color:#dc2626; border-bottom: 1px solid #dc2626; padding-bottom: 10px;'>Manual Cash Transaction Alert</h2>
                            <p style='color:#d1d5db;'>A manual cash entry was just added to the register. Please review the details below:</p>
                            
                            <table style='width:100%; max-width:600px; margin-top:20px; border-collapse: collapse; background-color:#111111; border: 1px solid #333333;'>
                                <tr>
                                    <td style='padding: 12px; border-bottom: 1px solid #333333; color:#9ca3af; width: 35%;'><strong>Transaction Type</strong></td>
                                    <td style='padding: 12px; border-bottom: 1px solid #333333; font-weight:bold; color:#ffffff; text-transform:uppercase;'>{dto.TransactionType}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px; border-bottom: 1px solid #333333; color:#9ca3af;'><strong>Amount</strong></td>
                                    <td style='padding: 12px; border-bottom: 1px solid #333333; font-weight:bold; font-size:18px; color:{(dto.Amount >= 0 ? "#3b82f6" : "#dc2626")};'>₹{dto.Amount}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px; border-bottom: 1px solid #333333; color:#9ca3af;'><strong>Reason / Note</strong></td>
                                    <td style='padding: 12px; border-bottom: 1px solid #333333; color:#facc15;'>{dto.Reason}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px; border-bottom: 1px solid #333333; color:#9ca3af;'><strong>Operator</strong></td>
                                    <td style='padding: 12px; border-bottom: 1px solid #333333; color:#ffffff;'>{opName}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px; border-bottom: 1px solid #333333; color:#9ca3af;'><strong>Branch</strong></td>
                                    <td style='padding: 12px; border-bottom: 1px solid #333333; color:#ffffff;'>{branchName}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 12px; color:#9ca3af;'><strong>Date / Time</strong></td>
                                    <td style='padding: 12px; color:#ffffff;'>{localTime:dd MMM yyyy, hh:mm tt} (IST)</td>
                                </tr>
                            </table>
                            
                            <p style='color: #6b7280; font-size: 13px; margin-top: 30px;'>This is an automated notification from Apple Esports ERP.</p>
                        </div>
                    ";
                    
                    foreach(var admin in superAdmins)
                    {
                        if (!string.IsNullOrEmpty(admin.Email))
                        {
                            await _emailService.SendEmailAsync(admin.Email, emailSubject, emailBody);
                        }
                    }
                }
            } 
            catch (Exception ex)
            {
                // We don't want the transaction to fail if email dispatch fails
                Console.WriteLine($"Failed to send cash transaction alert: {ex.Message}");
            }

            return MapToDto(register);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private static CashRegisterDto MapToDto(CashRegister r)
    {
        return new CashRegisterDto
        {
            Id = r.Id,
            ShiftId = r.ShiftId,
            BranchId = r.BranchId,
            OperatorId = r.OperatorId,
            OpeningBalance = r.OpeningBalance,
            TotalCashSales = r.TotalCashSales,
            TotalSplitCash = r.TotalSplitCash,
            ExpectedDrawerCash = r.ExpectedDrawerCash,
            PhysicalCashCounted = r.PhysicalCashCounted,
            CashDifference = r.CashDifference,
            MismatchReason = r.MismatchReason,
            Status = r.Status,
            OpenedAt = r.OpenedAt,
            VerifiedAt = r.VerifiedAt,
            ClosedAt = r.ClosedAt,
            Transactions = r.CashTransactions?.Select(tx => new CashTransactionDto
            {
                Id = tx.Id,
                BillId = tx.BillId,
                PcNumber = tx.PcNumber,
                CustomerName = tx.CustomerName,
                CashAmount = tx.CashAmount,
                GamingAmount = tx.GamingAmount,
                FoodAmount = tx.FoodAmount,
                TransactionType = tx.TransactionType,
                CreatedAt = tx.CreatedAt
            }).ToList() ?? new List<CashTransactionDto>()
        };
    }
}
