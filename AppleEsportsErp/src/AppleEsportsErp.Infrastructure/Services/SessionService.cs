using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Sessions;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _db;
    private readonly IHubNotificationService _hubNotifier;
    private readonly IAuditService _audit;
    private readonly IPcStatusService _pcStatus;
    private readonly ILogger<SessionService> _logger;
    private readonly IWalletService _walletService;

    public SessionService(
        IUnitOfWork uow,
        AppDbContext db,
        IHubNotificationService hubNotifier,
        IAuditService audit,
        IPcStatusService pcStatus,
        ILogger<SessionService> logger,
        IWalletService walletService)
    {
        _uow = uow;
        _db = db;
        _hubNotifier = hubNotifier;
        _audit = audit;
        _pcStatus = pcStatus;
        _logger = logger;
        _walletService = walletService;
    }

    public async Task<PaginatedResult<SessionDto>> GetActiveSessionsAsync(Guid branchId, int page, int pageSize)
    {
        var query = _db.Sessions
            .Include(s => s.Pc)
            .Include(s => s.Bills)
            .Where(s => s.BranchId == branchId && s.State == SessionState.Active)
            .OrderByDescending(s => s.StartTime);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var dtos = items.Select(s => new SessionDto
        {
            Id = s.Id,
            PcId = s.PcId,
            PcName = s.Pc?.PcNumber ?? "Unknown PC",
            BranchId = s.BranchId,
            OperatorId = s.OperatorId,
            ShiftId = s.ShiftId ?? Guid.Empty,
            CustomerName = s.CustomerName,
            MemberId = s.MemberId,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            DurationMinutes = s.ActualDurationMin ?? s.PlannedDurationMin ?? 0,
            ExpectedAmount = s.Bills.FirstOrDefault()?.TotalAmount ?? s.TotalAmount,
            PackageName = s.GamingType,
            Status = s.State,
            BillId = s.Bills.FirstOrDefault()?.Id ?? Guid.Empty
        }).ToList();

        return new PaginatedResult<SessionDto>(dtos, total, page, pageSize);
    }

    public async Task<SessionDto> StartSessionAsync(Guid branchId, Guid operatorId, Guid shiftId, SessionStartDto dto)
    {
        await _uow.BeginTransactionAsync();

        try
        {
            var pc = await _db.Pcs.FindAsync(dto.PcId);
            if (pc == null || pc.BranchId != branchId)
                throw new NotFoundException("PC not found or does not belong to this branch", "PC_NOT_FOUND");

            if (pc.State != PcState.Idle)
            {
                if (pc.State == PcState.Reserved)
                {
                    throw new AppException("This PC is reserved. Manual session creation is blocked.", System.Net.HttpStatusCode.BadRequest, "PC_RESERVED");
                }
                else
                {
                    throw new AppException($"Cannot start session. PC is currently {pc.State}", System.Net.HttpStatusCode.BadRequest, "PC_NOT_IDLE");
                }
            }

            var now = DateTimeOffset.UtcNow;
            
            var session = new Session
            {
                Id = Guid.NewGuid(),
                PcId = pc.Id,
                BranchId = branchId,
                OperatorId = operatorId,
                ShiftId = shiftId == Guid.Empty ? null : shiftId,
                CustomerName = dto.CustomerName,
                MemberId = dto.MemberId,
                StartTime = now,
                EndTime = dto.DurationMinutes > 0 ? now.AddMinutes((double)dto.DurationMinutes) : null,
                PlannedDurationMin = dto.DurationMinutes > 0 ? (int)dto.DurationMinutes : null,
                TotalAmount = dto.ExpectedAmount,
                GamingAmount = dto.ExpectedAmount,
                GamingType = dto.PackageName,
                State = SessionState.Active,
                Notes = dto.Notes,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _uow.Repository<Session>().AddAsync(session);

            // Wait, does BillStatus have Pending? Let's assume it does. 
            // The domain entities might differ slightly, let me verify it or use a default.
            // Let's create the bill directly.
            var bill = new Bill
            {
                Id = Guid.NewGuid(),
                BillNumber = $"BILL-{now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                SessionId = session.Id,
                PcId = pc.Id,
                BranchId = branchId,
                OperatorId = operatorId,
                ShiftId = shiftId == Guid.Empty ? null : shiftId,
                CustomerName = dto.CustomerName,
                MemberId = dto.MemberId,
                GamingAmount = dto.ExpectedAmount,
                FoodAmount = 0,
                Subtotal = dto.ExpectedAmount,
                TotalAmount = dto.ExpectedAmount,
                Status = BillStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _uow.Repository<Bill>().AddAsync(bill);

            var billItem = new BillItem
            {
                Id = Guid.NewGuid(),
                BillId = bill.Id,
                ItemType = "gaming",
                ItemName = $"Base Session ({dto.DurationMinutes}m)",
                Quantity = 1,
                UnitPrice = dto.ExpectedAmount,
                TotalPrice = dto.ExpectedAmount,
                CreatedAt = now
            };
            
            await _uow.Repository<BillItem>().AddAsync(billItem);

            pc.State = PcState.Active;
            pc.CurrentSessionId = session.Id;
            _uow.Repository<Pc>().Update(pc);

            await _uow.CommitTransactionAsync();

            await _audit.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = Roles.Operator,
                UserName = "System",
                Action = AuditActions.SessionStart,
                BranchId = branchId,
                TargetType = "session",
                TargetId = session.Id,
                Details = new { PcNumber = pc.PcNumber, dto.DurationMinutes, dto.ExpectedAmount }
            });

            await _hubNotifier.BroadcastPcStatusChangeAsync(branchId, pc.Id);
            await _hubNotifier.BroadcastSessionUpdateAsync(branchId, session.Id);
            await _hubNotifier.BroadcastBillingUpdateAsync(branchId, bill.Id);
            
            // Dispatch Unlock Command to the actual PC Agent
            await _hubNotifier.SendUnlockCommandToAgentAsync(pc.Id, (int)dto.DurationMinutes, dto.CustomerName);

            return new SessionDto
            {
                Id = session.Id,
                PcId = pc.Id,
                PcName = pc.PcNumber,
                BranchId = branchId,
                OperatorId = operatorId,
                ShiftId = shiftId,
                CustomerName = dto.CustomerName,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                DurationMinutes = dto.DurationMinutes,
                ExpectedAmount = dto.ExpectedAmount,
                PackageName = dto.PackageName,
                Status = session.State,
                BillId = bill.Id
            };
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<SessionDto> StopSessionAsync(Guid branchId, Guid operatorId, Guid sessionId, bool deferPayment = false)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            var session = await _db.Sessions
                .Include(s => s.Pc)
                .Include(s => s.Bills)
                    .ThenInclude(b => b.Items)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.BranchId == branchId);

            if (session == null)
                throw new NotFoundException("Session not found", "SESSION_NOT_FOUND");

            if (session.State != SessionState.Active)
                throw new AppException("Session is already ended.", System.Net.HttpStatusCode.BadRequest, "SESSION_ALREADY_ENDED");

            var now = DateTimeOffset.UtcNow;
            session.State = SessionState.Completed;
            session.UpdatedAt = now;
            session.EndTime = now;
            session.ActualDurationMin = (int)(now - session.StartTime).TotalMinutes;
            
            var bill = session.Bills.FirstOrDefault();

            // 1. If Open Session, Calculate Cost
            if (session.PlannedDurationMin == null)
            {
                // Fetch global Hz pricing
                var globalConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == "global_system_rules");
                decimal defaultBaseRate = 100m;
                Dictionary<string, decimal> hzPricing = new();
                if (globalConfig != null && !string.IsNullOrEmpty(globalConfig.ConfigValue))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(globalConfig.ConfigValue);
                        if (doc.RootElement.TryGetProperty("pricing", out var pricing))
                        {
                            if (pricing.TryGetProperty("baseRate", out var br))
                                defaultBaseRate = br.GetDecimal();
                                
                            if (pricing.TryGetProperty("hzPricing", out var hzObj))
                            {
                                foreach (var prop in hzObj.EnumerateObject())
                                {
                                    if (prop.Value.ValueKind == JsonValueKind.Number)
                                        hzPricing[prop.Name] = prop.Value.GetDecimal();
                                }
                            }
                        }
                    }
                    catch { /* fallback */ }
                }

                decimal ratePerHour = defaultBaseRate;
                if (!string.IsNullOrEmpty(session.Pc?.MonitorHz) && hzPricing.TryGetValue(session.Pc.MonitorHz, out var hrRate))
                {
                    ratePerHour = hrRate;
                }

                // If member, apply a flat discount or specific member rate if defined, otherwise maybe same rate? 
                // The user requested standardizing the rate based on Hz. We'll use the Hz rate.
                // Optionally apply discount logic here later.

                decimal hours = Math.Max((decimal)session.ActualDurationMin.Value / 60m, 1m / 60m); // Min 1 min charge
                session.GamingAmount = Math.Round(hours * ratePerHour, 2);
                session.TotalAmount = session.GamingAmount + session.FoodAmount;

                if (bill != null)
                {
                    bill.GamingAmount = session.GamingAmount;
                    bill.Subtotal = session.TotalAmount;
                    bill.TotalAmount = session.TotalAmount;
                    
                    var gamingItem = bill.Items.FirstOrDefault(i => i.ItemType == "gaming");
                    if (gamingItem != null)
                    {
                        gamingItem.ItemName = $"Open Session ({session.ActualDurationMin}m)";
                        gamingItem.TotalPrice = session.GamingAmount;
                        gamingItem.UnitPrice = session.GamingAmount;
                    }
                }
            }

            // 2. Wallet Deduction for Members is now handled manually via Overlay Approval
            // The session goes to Completed, bill stays Pending, PC goes to AwaitingBilling.
            
            var pc = session.Pc!;

            var hasUnpaidBill = session.Bills.Any(b => b.Status != BillStatus.Completed);

            if (!hasUnpaidBill || deferPayment)
            {
                // Paid (member wallet) or operator deferred — free the PC immediately
                pc.State = PcState.Idle;
            }
            else
            {
                // Customer at counter waiting to pay
                pc.State = PcState.AwaitingBilling;
            }

            // Mark bill as deferred so payment is backdated to session date
            if (deferPayment && bill != null && bill.Status != BillStatus.Completed)
            {
                bill.IsDeferred = true;
                bill.Status = BillStatus.Completed; // It leaves the billing counter
                
                var customerCredit = new CustomerCredit
                {
                    BranchId = branchId,
                    OperatorId = operatorId,
                    BillId = bill.Id,
                    CustomerName = session.CustomerName ?? session.Member?.Username ?? "Walk-in",
                    CustomerPhone = session.Member?.MobileNumber ?? "N/A",
                    PcNumber = pc.PcNumber ?? "N/A",
                    OriginalBillAmount = bill.TotalAmount,
                    AmountPaidInitially = 0,
                    CreditAmount = bill.TotalAmount,
                    Status = "pending",
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _uow.Repository<CustomerCredit>().AddAsync(customerCredit);
            }

            pc.CurrentSessionId = null;
            
            _uow.Repository<Session>().Update(session);
            if (bill != null) _uow.Repository<Bill>().Update(bill);
            _uow.Repository<Pc>().Update(pc);
            
            await _uow.CommitTransactionAsync();

            await _audit.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = Roles.Operator,
                UserName = "System",
                Action = AuditActions.SessionStop,
                BranchId = branchId,
                TargetType = "session",
                TargetId = session.Id,
                Details = new { PcNumber = pc.PcNumber, session.TotalAmount }
            });

            await _hubNotifier.BroadcastPcStatusChangeAsync(branchId, pc.Id);
            await _hubNotifier.BroadcastSessionUpdateAsync(branchId, session.Id);
            if (session.Bills.Any())
            {
                await _hubNotifier.BroadcastBillingUpdateAsync(branchId, session.Bills.First().Id);
            }
            
            // Dispatch Lock Command to the actual PC Agent
            await _hubNotifier.SendLockCommandToAgentAsync(pc.Id);

            return new SessionDto
            {
                Id = session.Id,
                PcId = pc.Id,
                PcName = pc.PcNumber,
                BranchId = branchId,
                OperatorId = session.OperatorId,
                ShiftId = session.ShiftId ?? Guid.Empty,
                CustomerName = session.CustomerName,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                DurationMinutes = session.ActualDurationMin ?? 0,
                ExpectedAmount = session.TotalAmount,
                PackageName = session.GamingType,
                Status = session.State,
                BillId = session.Bills.FirstOrDefault()?.Id ?? Guid.Empty
            };
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<SessionDto> ExtendSessionAsync(Guid branchId, Guid operatorId, Guid sessionId, SessionExtendDto dto)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            var session = await _db.Sessions
                .Include(s => s.Pc)
                .Include(s => s.Bills)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.BranchId == branchId);

            if (session == null)
                throw new NotFoundException("Session not found", "SESSION_NOT_FOUND");

            if (session.State != SessionState.Active)
                throw new AppException("Cannot extend inactive session.", System.Net.HttpStatusCode.BadRequest, "SESSION_NOT_ACTIVE");

            var now = DateTimeOffset.UtcNow;
            
            session.PlannedDurationMin = (session.PlannedDurationMin ?? 0) + (int)dto.AdditionalMinutes;
            if (session.EndTime.HasValue)
            {
                session.EndTime = session.EndTime.Value.AddMinutes((double)dto.AdditionalMinutes);
            }
            session.GamingAmount += dto.AdditionalAmount;
            session.TotalAmount += dto.AdditionalAmount;
            
            var newGamingType = $"{session.GamingType} + {dto.PackageName}";
            if (newGamingType.Length > 50)
            {
                newGamingType = newGamingType.Substring(0, 47) + "...";
            }
            session.GamingType = newGamingType;
            
            session.UpdatedAt = now;

            var bill = session.Bills.FirstOrDefault();
            if (bill != null)
            {
                bill.GamingAmount += dto.AdditionalAmount;
                bill.Subtotal += dto.AdditionalAmount;
                bill.TotalAmount += dto.AdditionalAmount;
                bill.UpdatedAt = now;
                _uow.Repository<Bill>().Update(bill);

                var extendItem = new BillItem
                {
                    Id = Guid.NewGuid(),
                    BillId = bill.Id,
                    ItemType = "gaming",
                    ItemName = dto.PackageName, // "Extension - 60m"
                    Quantity = 1,
                    UnitPrice = dto.AdditionalAmount,
                    TotalPrice = dto.AdditionalAmount,
                    CreatedAt = now
                };
                
                await _uow.Repository<BillItem>().AddAsync(extendItem);
            }

            _uow.Repository<Session>().Update(session);
            await _uow.CommitTransactionAsync();

            await _audit.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = Roles.Operator,
                UserName = "System",
                Action = AuditActions.SessionExtend,
                BranchId = branchId,
                TargetType = "session",
                TargetId = session.Id,
                Details = new { dto.AdditionalMinutes, dto.AdditionalAmount, PcNumber = session.Pc?.PcNumber }
            });

            await _hubNotifier.BroadcastSessionUpdateAsync(branchId, session.Id);
            await _hubNotifier.BroadcastPcStatusChangeAsync(branchId, session.PcId);
            if (bill != null)
            {
                await _hubNotifier.BroadcastBillingUpdateAsync(branchId, bill.Id);
            }

            return new SessionDto
            {
                Id = session.Id,
                PcId = session.PcId,
                PcName = session.Pc?.PcNumber ?? "Unknown",
                BranchId = branchId,
                OperatorId = session.OperatorId,
                ShiftId = session.ShiftId ?? Guid.Empty,
                CustomerName = session.CustomerName,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                DurationMinutes = session.PlannedDurationMin ?? 0,
                ExpectedAmount = session.TotalAmount,
                PackageName = session.GamingType,
                Status = session.State,
                BillId = bill?.Id ?? Guid.Empty
            };
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<SessionDto> TransferSessionAsync(Guid branchId, Guid operatorId, Guid sessionId, SessionTransferDto dto)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            var session = await _db.Sessions
                .Include(s => s.Pc)
                .Include(s => s.Bills)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.BranchId == branchId);

            if (session == null)
                throw new NotFoundException("Session not found", "SESSION_NOT_FOUND");

            if (session.State != SessionState.Active)
                throw new AppException("Only active sessions can be transferred.", System.Net.HttpStatusCode.BadRequest, "SESSION_NOT_ACTIVE");

            var targetPc = await _db.Pcs.FirstOrDefaultAsync(p => p.Id == dto.TargetPcId && p.BranchId == branchId);
            if (targetPc == null)
                throw new NotFoundException("Target PC not found", "PC_NOT_FOUND");

            if (targetPc.State != PcState.Idle)
                throw new AppException($"Target PC is currently {targetPc.State}", System.Net.HttpStatusCode.BadRequest, "PC_NOT_IDLE");

            var oldPc = session.Pc!;
            
            session.PcId = targetPc.Id;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            
            var transferLog = $"[Transfer: {oldPc.PcName ?? oldPc.PcNumber} -> {targetPc.PcName ?? targetPc.PcNumber} at {DateTimeOffset.Now:HH:mm}]";
            session.Notes = string.IsNullOrEmpty(session.Notes) ? transferLog : session.Notes + "\n" + transferLog;

            oldPc.State = PcState.Idle;
            oldPc.CurrentSessionId = null;
            targetPc.State = PcState.Active;
            targetPc.CurrentSessionId = session.Id;

            var bill = session.Bills.FirstOrDefault();
            if (bill != null)
            {
                bill.PcId = targetPc.Id;
                bill.UpdatedAt = DateTimeOffset.UtcNow;
                _uow.Repository<Bill>().Update(bill);
            }

            _uow.Repository<Session>().Update(session);
            _uow.Repository<Pc>().Update(oldPc);
            _uow.Repository<Pc>().Update(targetPc);
            
            await _uow.CommitTransactionAsync();

            await _audit.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = Roles.Operator,
                UserName = "System",
                Action = AuditActions.SessionTransfer,
                BranchId = branchId,
                TargetType = "session",
                TargetId = session.Id,
                Details = new { from = oldPc.PcNumber, to = targetPc.PcNumber }
            });

            await _hubNotifier.BroadcastPcStatusChangeAsync(branchId, oldPc.Id);
            await _hubNotifier.BroadcastPcStatusChangeAsync(branchId, targetPc.Id);
            await _hubNotifier.BroadcastSessionUpdateAsync(branchId, session.Id);

            if (bill != null)
            {
                await _hubNotifier.BroadcastBillingUpdateAsync(branchId, bill.Id);
            }

            return new SessionDto
            {
                Id = session.Id,
                PcId = session.PcId,
                PcName = targetPc.PcNumber,
                BranchId = branchId,
                OperatorId = session.OperatorId,
                ShiftId = session.ShiftId ?? Guid.Empty,
                CustomerName = session.CustomerName,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                DurationMinutes = session.PlannedDurationMin ?? 0,
                ExpectedAmount = session.TotalAmount,
                PackageName = session.GamingType,
                Status = session.State,
                BillId = bill?.Id ?? Guid.Empty
            };
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }
}
