using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Cash;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.Services;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class CashRegisterService : ICashRegisterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IHubNotificationService _hubNotification;
    private readonly IEmailService _emailService;
    private readonly IAdminNotifier _adminNotifier;
    private readonly ILogger<CashRegisterService> _logger;

    public CashRegisterService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IHubNotificationService hubNotification,
        IEmailService emailService,
        IAdminNotifier adminNotifier,
        ILogger<CashRegisterService> logger)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _hubNotification = hubNotification;
        _emailService = emailService;
        _adminNotifier = adminNotifier;
        _logger = logger;
    }

    /// <summary>
    /// THIS shift's drawer - the one this operator's own login actually opened or resumed, by
    /// its real identity (ShiftId), never "whichever register merely looks newest."
    ///
    /// Used to be scoped by status only ("any non-Closed register for the branch, newest
    /// first") on the reasoning that there is one physical cash box, so any open register must
    /// be the right one. That reasoning breaks the moment more than one non-Closed register
    /// exists for the branch at once - which a stray double-click (Start Verification fired
    /// twice, or Open Register fired twice) is enough to cause - because "newest" then silently
    /// starts pointing at whichever accidental extra register was created last, not at the real
    /// one this shift has actually been trading against. Confirmed live: a shift's real
    /// register, holding genuine sales, got locked into Verifying by a double-click; every
    /// screen that asked "what's the current register?" kept answering with a second, empty
    /// register created moments later instead - Expected Drawer Total read ₹0, cash payments
    /// were refused, and nothing about the real register's own numbers had actually changed.
    ///
    /// ShiftId is safe to pin to rather than a regression: the same operator logging back in
    /// mid-shift resumes their existing Shift row rather than getting a new one (see
    /// AuthService.LoginOperatorAsync), so this still "carries on with the same drawer" across
    /// a re-login exactly as before - it just can no longer be fooled by an accidental extra
    /// register that was never actually this shift's own.
    /// </summary>
    public async Task<CashRegisterDto> GetActiveRegisterAsync(Guid branchId, Guid shiftId)
    {
        var register = await _unitOfWork.Repository<CashRegister>().Query()
            .Include(r => r.CashTransactions)
            .Where(r => r.BranchId == branchId && r.ShiftId == shiftId && r.Status != CashRegisterStatus.Closed)
            .OrderByDescending(r => r.OpenedAt)
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException("No cash register has been opened for today yet.");

        return MapToDto(register);
    }

    /// <summary>
    /// Which opening question the shift-start screen should ask.
    ///
    /// Reads exactly what <see cref="OpenRegisterAsync"/> will do rather than guessing at it, so
    /// the figure shown to the operator is the figure the drawer opens with. Two rules that
    /// disagree is how the branch ended up with two registers for one drawer in the first place.
    ///
    /// Not scoped by calendar day: the most recent register for the branch is the one that
    /// matters, whatever day it was opened on — a register still open from before midnight must
    /// not be treated as "nothing open yet" the instant the date rolls over.
    /// </summary>
    public async Task<RegisterOpeningDto> GetOpeningAsync(Guid branchId)
    {
        var lastRegister = await _unitOfWork.Repository<CashRegister>().Query()
            .Where(r => r.BranchId == branchId)
            .OrderByDescending(r => r.OpenedAt)
            .FirstOrDefaultAsync();

        if (lastRegister is null)
            return new RegisterOpeningDto { IsFirstOfDay = true };

        if (lastRegister.Status == CashRegisterStatus.Open)
            return new RegisterOpeningDto
            {
                AlreadyOpen = true,
                InheritedBalance = lastRegister.ExpectedDrawerCash,
            };

        return new RegisterOpeningDto
        {
            IsFirstOfDay = await WasLastShiftCloseAsync(lastRegister),
            InheritedBalance = lastRegister.PhysicalCashCounted ?? lastRegister.ExpectedDrawerCash,
        };
    }

    /// <summary>
    /// Whether the shift this register belonged to was closed on a "last shift of the day" tick
    /// - the point at which the day's cash is normally taken out and handed to the owner, so
    /// nothing about what was in the drawer then says anything about what is in it now. Read
    /// straight off Shift.ClosedTradingDay rather than guessed at from timing, because that flag
    /// is the one place the operator actually said "the day is over" - a long gap since the last
    /// close is just as consistent with the shop having simply been quiet.
    /// </summary>
    private async Task<bool> WasLastShiftCloseAsync(CashRegister lastRegister)
    {
        var shift = await _unitOfWork.Repository<Shift>().Query()
            .Where(s => s.Id == lastRegister.ShiftId)
            .Select(s => new { s.ClosedTradingDay })
            .FirstOrDefaultAsync();

        return shift?.ClosedTradingDay == true;
    }

    public async Task<OpenRegisterResultDto> OpenRegisterAsync(Guid branchId, Guid operatorId, Guid shiftId, OpenRegisterDto dto)
    {
        var today = IndiaTime.BusinessDayOf(DateTimeOffset.UtcNow);

        // The most recent drawer for this branch, whatever state it is in and whatever day it
        // was opened on. Matching only on Open was the bug: an operator who ended their shift
        // and logged back in found nothing open, was asked for a fresh float, and got a SECOND
        // drawer for the same till. One held Rs 100 and one held Rs 0, the end-of-day screen
        // read one and the lock screen the other, and nothing compared them.
        //
        // Matching on BusinessDay == today was a second, later version of the same bug: a
        // register opened before midnight and still genuinely open after it stopped being found
        // at all the moment the calendar date rolled over, so a branch open past midnight got
        // asked to open a brand new drawer mid-shift. BusinessDay is still stamped on the
        // register below when a new one is actually opened - it is only this lookup that no
        // longer filters by it.
        //
        // In the branch EXE this needs no reload to happen. The prompt is remembered in
        // sessionStorage, which is wiped whenever the app closes - so it returns after every
        // restart, which means after every power cut.
        var lastRegister = await _unitOfWork.Repository<CashRegister>().Query()
            .Where(r => r.BranchId == branchId)
            .OrderByDescending(r => r.OpenedAt)
            .FirstOrDefaultAsync();

        // Still open: hand back the same drawer rather than opening a rival to it.
        if (lastRegister != null && lastRegister.Status == CashRegisterStatus.Open)
            return new OpenRegisterResultDto { Opened = true, Register = MapToDto(lastRegister) };

        // A branch has ONE drawer and it runs through the trading day, but "inherit what the
        // last shift left" is only ever a fact worth trusting when nothing has happened to the
        // drawer since - the same physical till, still in the shop, still nobody's had reason to
        // touch it. Two things break that: no previous register at all (there is nothing to
        // inherit), and the last one closing on a "last shift of the day" tick, which is normally
        // exactly when the cash comes out and goes to the owner. Either way there is nothing
        // honest to suggest, so the operator counts from nothing instead of being handed a number
        // that used to be true.
        var needsFreshCount = lastRegister is null || await WasLastShiftCloseAsync(lastRegister);

        decimal? expected = needsFreshCount
            ? null
            : (lastRegister!.PhysicalCashCounted ?? lastRegister.ExpectedDrawerCash);

        // A real count that disagrees with what was expected is not opened silently - the
        // operator is handed the difference back and has to say why before the drawer opens.
        // Sent back rather than thrown, the same shape ShiftTakeoverService.SubmitCountAsync
        // already uses for the identical situation: this is not an error, it is a question that
        // has not been answered yet.
        if (expected.HasValue && expected.Value != dto.OpeningBalance && string.IsNullOrWhiteSpace(dto.Reason))
        {
            return new OpenRegisterResultDto
            {
                Opened = false,
                ExpectedBalance = expected.Value,
                CountedBalance = dto.OpeningBalance,
                Difference = dto.OpeningBalance - expected.Value,
            };
        }

        // Always what the operator actually counted, never what was merely expected - the
        // opposite of what this used to do, and the entire point of asking.
        var openingBalance = dto.OpeningBalance;
        var mismatch = expected.HasValue && expected.Value != openingBalance;

        var register = new CashRegister
        {
            BranchId = branchId,
            OperatorId = operatorId,
            ShiftId = shiftId,
            BusinessDay = today,
            OpeningBalance = openingBalance,
            ExpectedDrawerCash = openingBalance,
            TotalCashSales = 0,
            TotalSplitCash = 0,
            Status = CashRegisterStatus.Open,
            OpenedAt = DateTimeOffset.UtcNow,
            MismatchReason = mismatch
                ? $"Opening count did not match what was expected (₹{expected:0.00} expected, ₹{openingBalance:0.00} counted). {dto.Reason}"
                : null,
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
            Details = mismatch
                ? new { OpeningBalance = openingBalance, ExpectedBalance = expected, Reason = dto.Reason }
                : new { OpeningBalance = openingBalance }
        });

        await _unitOfWork.CommitTransactionAsync();
        await _hubNotification.BroadcastCashRegisterUpdateAsync(branchId, register.Id);

        if (mismatch)
            await NotifyOwnerOfOpeningMismatchAsync(branchId, operatorId, expected!.Value, openingBalance, dto.Reason!);

        return new OpenRegisterResultDto { Opened = true, Register = MapToDto(register) };
    }

    /// <summary>
    /// Tells the owner the same way ShiftTakeoverService does for an abandoned handover - who,
    /// where, expected vs counted, and their own words for the gap. Never allowed to undo the
    /// register actually opening: the drawer is real and the operator is waiting on it.
    /// </summary>
    private async Task NotifyOwnerOfOpeningMismatchAsync(
        Guid branchId, Guid operatorId, decimal expected, decimal counted, string reason)
    {
        try
        {
            var operatorName = await _unitOfWork.Repository<Operator>().Query()
                .Where(o => o.Id == operatorId).Select(o => o.FullName).FirstOrDefaultAsync() ?? "Unknown operator";
            var branchName = await _unitOfWork.Repository<Branch>().Query()
                .Where(b => b.Id == branchId).Select(b => b.Name).FirstOrDefaultAsync() ?? "Unknown branch";

            var difference = counted - expected;
            var isShort = difference < 0;
            var amount = Math.Abs(difference);

            await _adminNotifier.NotifyAsync(
                $"Cash {(isShort ? "short" : "over")} at opening by ₹{amount:N0} - {operatorName} at {branchName}",
                AdminEmailTemplate.Compose(
                    heading: "An opening count did not match what was expected",
                    accent: AdminEmailTemplate.Red,
                    summary: $"{operatorName} opened the drawer at {branchName} and counted a different amount " +
                              "than the system expected from the last shift.",
                    rows: new List<(string, string)>
                    {
                        ("Branch", branchName),
                        ("Opened by", operatorName),
                        ("", ""),
                        ("Expected in the drawer", $"₹{expected:N2}"),
                        ("Actually counted", $"₹{counted:N2}"),
                        (isShort ? "Missing" : "Extra", $"₹{amount:N2}"),
                        ("", ""),
                        ("Reason given", reason),
                    },
                    headline: $"₹{amount:N2} {(isShort ? "short" : "over")}",
                    footnote: "The register has been opened with the counted figure, not the expected one - " +
                               "what is actually in the drawer is what the shift starts from."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send the opening-mismatch email for branch {BranchId}.", branchId);
        }
    }

    public async Task<CashRegisterDto> AddTransactionAsync(Guid branchId, Guid operatorId, Guid shiftId, AddCashTransactionDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // The branch's currently open drawer, whoever opened it and whatever day it was
            // opened on — cash taken after a re-login belongs in the same box it physically
            // went into.
            var register = await _unitOfWork.Repository<CashRegister>().Query()
                .Include(r => r.CashTransactions)
                .FirstOrDefaultAsync(r => r.BranchId == branchId
                                       && r.Status == CashRegisterStatus.Open)
                ?? throw new NotFoundException("No cash register has been opened for today yet.");

            // The direction is decided here, from what kind of movement this is. It is never
            // taken from the sign the caller happened to send.
            //
            // This used to be "ExpectedDrawerCash += dto.Amount" with a comment saying
            // withdrawals decrease it - true only if whoever called remembered to pass a
            // negative number. A Rs 5,000 handover to the owner entered as 5000 RAISED the
            // expected drawer by Rs 5,000 instead of lowering it: a Rs 10,000 error, and the
            // operator shows short by exactly the amount they correctly sent up.
            var magnitude = Math.Abs(dto.Amount);
            if (magnitude == 0)
                throw new AppException("Enter an amount.");

            var signedAmount = dto.TransactionType switch
            {
                "inward" => magnitude,            // cash added to the drawer
                "petty_expense" => -magnitude,    // spent out of the drawer
                "withdrawal" => -magnitude,       // taken out and sent to the owner
                _ => throw new AppException(
                        $"'{dto.TransactionType}' is not a kind of cash movement this can record."),
            };

            // Taking out more than the drawer holds is a typing mistake, not a transaction.
            // Letting it through leaves a negative expected drawer, which nobody can reconcile.
            if (signedAmount < 0 && magnitude > register.ExpectedDrawerCash)
                throw new AppException(
                    $"The drawer only has Rs {register.ExpectedDrawerCash:0.00} in it, so Rs {magnitude:0.00} cannot be taken out.");

            var tx = new CashTransaction
            {
                CashRegisterId = register.Id,
                BranchId = branchId,
                OperatorId = operatorId,
                CashAmount = signedAmount,
                CashReceived = signedAmount,
                ChangeReturned = 0,
                ActualCashCollected = signedAmount,
                GamingAmount = 0,
                FoodAmount = 0,
                CustomerName = dto.Reason ?? "Operator Adjustment",
                TransactionType = dto.TransactionType,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<CashTransaction>().AddAsync(tx);

            register.ExpectedDrawerCash += signedAmount;
            
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
                    CashReceived = tx.CashReceived,
                    ChangeReturned = tx.ChangeReturned,
                    ActualCashCollected = tx.ActualCashCollected,
                    GamingAmount = tx.GamingAmount,
                    FoodAmount = tx.FoodAmount,
                    TransactionType = tx.TransactionType,
                    CreatedAt = tx.CreatedAt
                }).ToList() ?? new List<CashTransactionDto>()
        };
    }
}
