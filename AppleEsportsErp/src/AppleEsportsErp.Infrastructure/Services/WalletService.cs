using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Wallets;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class WalletService : IWalletService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;

    public WalletService(IUnitOfWork unitOfWork, IAuditService auditService, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _emailService = emailService;
    }

    public async Task<WalletTransactionDto> TopUpWalletAsync(Guid branchId, Guid operatorId, Guid shiftId, Guid memberId, TopUpWalletDto dto)
    {
        if (dto.Amount <= 0)
            throw new AppException("Top-up amount must be greater than zero.");

        var member = await _unitOfWork.Repository<Member>().GetByIdAsync(memberId)
            ?? throw new NotFoundException("Member not found.");

        var isGaming = dto.TargetWallet == WalletType.Gaming;
        var balanceBefore = isGaming ? member.GamingBalance : member.FoodBalance;
        
        if (isGaming)
            member.GamingBalance += dto.Amount;
        else
            member.FoodBalance += dto.Amount;
            
        var balanceAfter = isGaming ? member.GamingBalance : member.FoodBalance;

        _unitOfWork.Repository<Member>().Update(member);

        var walletTx = new WalletTransaction
        {
            MemberId = memberId,
            BranchId = branchId,
            OperatorId = operatorId,
            Action = WalletAction.Recharge,
            TargetWallet = dto.TargetWallet,
            Amount = dto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            PaymentType = dto.PaymentType,
            CashAmount = dto.PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase) ? dto.Amount : 0,
            OnlineAmount = dto.PaymentType.Equals("Online", StringComparison.OrdinalIgnoreCase) ? dto.Amount : 0,
            Reason = dto.Reason ?? "Manual Top-Up",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<WalletTransaction>().AddAsync(walletTx);

        // If Cash, we must also update the Cash Register (SOP §10.3)
        if (walletTx.CashAmount > 0)
        {
            var activeRegister = await _unitOfWork.Repository<CashRegister>().Query()
                .FirstOrDefaultAsync(cr => cr.BranchId == branchId && cr.ShiftId == shiftId && cr.Status == CashRegisterStatus.Open);
                
            if (activeRegister == null)
                throw new AppException("Cannot process cash top-up: No active cash register found for this shift.");

            activeRegister.ExpectedDrawerCash += walletTx.CashAmount;
            _unitOfWork.Repository<CashRegister>().Update(activeRegister);

            var cashTx = new CashTransaction
            {
                CashRegisterId = activeRegister.Id,
                BranchId = branchId,
                OperatorId = operatorId,
                TransactionType = "wallet_recharge",
                CashAmount = walletTx.CashAmount,
                GamingAmount = 0,
                FoodAmount = 0,
                CustomerName = member.Username,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _unitOfWork.Repository<CashTransaction>().AddAsync(cashTx);
        }

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = AuditActions.WalletRecharge,
            BranchId = branchId,
            TargetType = "wallet",
            TargetId = member.Id,
            Details = new { Amount = dto.Amount, PaymentType = dto.PaymentType }
        });

        await _unitOfWork.CommitTransactionAsync();

        // SOP: Initial Top-Up Welcome Email Automation
        // If they have Login Access (Username) and an Email, but haven't set a Password yet,
        // we automatically send them the Password Setup link during their initial top-ups.
        if (!string.IsNullOrWhiteSpace(member.Username) && 
            !string.IsNullOrWhiteSpace(member.Email) && 
            string.IsNullOrWhiteSpace(member.PasswordHash))
        {
            // Only send if they don't already have a valid, active reset token
            if (string.IsNullOrWhiteSpace(member.ResetToken) || member.ResetTokenExpiry < DateTimeOffset.UtcNow)
            {
                var setupToken = Guid.NewGuid().ToString("N");
                member.ResetToken = setupToken;
                member.ResetTokenExpiry = DateTimeOffset.UtcNow.AddHours(24);
                
                _unitOfWork.Repository<Member>().Update(member);
                await _unitOfWork.SaveChangesAsync(); // save token

                string resetLink = $"http://localhost:5173/reset-password?email={member.Email}&token={setupToken}";
                string subject = "Welcome to Apple Esports - Setup Your Password";
                string welcomeBody = $@"
                <div style='background-color:#050505; color:#ffffff; font-family:""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; padding:40px 20px; text-align:center;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: #111111; border: 1px solid #333333; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.5);'>
                        <div style='background: linear-gradient(135deg, #1a1a24 0%, #0d0d14 100%); padding: 30px 20px; border-bottom: 2px solid #3b82f6;'>
                            <h1 style='margin: 0; font-size: 28px; letter-spacing: 2px; color: #ffffff; text-transform: uppercase;'>
                                <img src='https://appleesports.in/apple-touch-icon.png' alt='Logo' style='height: 40px; vertical-align: middle; margin-right: 15px;' /> APPLE ESPORTS
                            </h1>
                        </div>
                        <div style='padding: 40px 30px; text-align: left;'>
                            <h2 style='margin-top: 0; color: #3b82f6; font-size: 24px; border-bottom: 2px solid #333333; padding-bottom: 15px;'>Welcome to Apple Esports!</h2>
                            <p style='font-size: 16px; color: #d1d5db; line-height: 1.6;'>Hi <strong>{member.FullName}</strong>,</p>
                            <p style='font-size: 16px; color: #d1d5db; line-height: 1.6;'>Your Apple Esports membership login has been activated following your initial top-up! To log in and use your wallet for food or gaming, you need to set up a secure password.</p>
                            <div style='text-align:center; margin: 40px 0;'>
                                <a href='{resetLink}' style='background: linear-gradient(to right, #2563eb, #3b82f6); color: #ffffff; padding: 16px 32px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; letter-spacing: 1px; display: inline-block; box-shadow: 0 4px 15px rgba(59, 130, 246, 0.4);'>SET MY PASSWORD</a>
                            </div>
                            <p style='color: #6b7280; font-size: 13px; margin-top: 30px;'>This link will expire in 24 hours. If you did not request this, please contact the branch operator.</p>
                        </div>
                        <div style='background-color: #080808; padding: 20px; border-top: 1px solid #222222; text-align: center;'>
                            <p style='margin: 0; color: #6b7280; font-size: 12px;'>This is an automated notification from Apple Esports ERP.</p>
                        </div>
                    </div>
                </div>";

                await _emailService.SendEmailAsync(member.Email, subject, welcomeBody);
            }
        }

        return MapToDto(walletTx);
    }

    public async Task<WalletTransactionDto> DeductWalletAsync(Guid branchId, Guid operatorId, Guid memberId, DeductWalletDto dto)
    {
        if (dto.Amount <= 0)
            throw new AppException("Deduction amount must be greater than zero.");

        var member = await _unitOfWork.Repository<Member>().GetByIdAsync(memberId)
            ?? throw new NotFoundException("Member not found.");

        var isGaming = dto.TargetWallet == WalletType.Gaming;
        var balanceBefore = isGaming ? member.GamingBalance : member.FoodBalance;

        if (balanceBefore < dto.Amount)
            throw new AppException($"Insufficient {dto.TargetWallet} wallet balance. Current: {balanceBefore}, Required: {dto.Amount}");

        if (isGaming)
            member.GamingBalance -= dto.Amount;
        else
            member.FoodBalance -= dto.Amount;

        var balanceAfter = isGaming ? member.GamingBalance : member.FoodBalance;

        _unitOfWork.Repository<Member>().Update(member);

        var walletTx = new WalletTransaction
        {
            MemberId = memberId,
            BranchId = branchId,
            OperatorId = operatorId,
            Action = WalletAction.Correction,
            TargetWallet = dto.TargetWallet,
            Amount = dto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            PaymentType = "Wallet",
            CashAmount = 0,
            OnlineAmount = 0,
            BillId = dto.BillId,
            Reason = dto.Reason,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<WalletTransaction>().AddAsync(walletTx);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = AuditActions.WalletDeduction,
            BranchId = branchId,
            TargetType = "wallet",
            TargetId = member.Id,
            Details = new { Amount = dto.Amount, Reason = dto.Reason }
        });

        await _unitOfWork.CommitTransactionAsync();

        return MapToDto(walletTx);
    }

    public async Task<PaginatedResult<WalletTransactionDto>> GetWalletHistoryAsync(Guid memberId, int page = 1, int pageSize = 50)
    {
        var query = _unitOfWork.Repository<WalletTransaction>().Query()
            .Where(w => w.MemberId == memberId)
            .OrderByDescending(w => w.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var dtos = items.Select(MapToDto).ToList();
        return new PaginatedResult<WalletTransactionDto>(dtos, total, page, pageSize);
    }

    private static WalletTransactionDto MapToDto(WalletTransaction t)
    {
        return new WalletTransactionDto
        {
            Id = t.Id,
            MemberId = t.MemberId,
            Action = t.Action,
            TargetWallet = t.TargetWallet,
            Amount = t.Amount,
            BalanceBefore = t.BalanceBefore,
            BalanceAfter = t.BalanceAfter,
            PaymentType = t.PaymentType,
            Reason = t.Reason,
            CreatedAt = t.CreatedAt
        };
    }
}
