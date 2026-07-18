using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.PcManagement;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class PcManagementService : IPcManagementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IHubNotificationService _hubNotification;

    public PcManagementService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IHubNotificationService hubNotification)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _hubNotification = hubNotification;
    }

    public async Task<List<PcDto>> GetPcsByBranchAsync(Guid branchId, bool includeDeleted = false)
    {
        var query = _unitOfWork.Repository<Pc>().Query()
            .Include(p => p.PricingProfile)
            .Where(p => p.BranchId == branchId);

        if (!includeDeleted)
        {
            query = query.Where(p => !p.IsDeleted);
        }

        var pcs = await query.ToListAsync();
        return pcs.Select(MapToDto).ToList();
    }

    public async Task<PcDto> AddPcAsync(Guid branchId, Guid superAdminId, CreatePcDto dto)
    {
        var existing = await _unitOfWork.Repository<Pc>().Query()
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.PcNumber == dto.PcNumber && !p.IsDeleted);

        if (existing != null)
            throw new AppException($"PC with number {dto.PcNumber} already exists in this branch.");

        if (!dto.PricingProfileId.HasValue)
            throw new AppException("A Pricing Profile is required. Select one (or create one in Settings → Pricing Profiles first) before adding this PC.");

        var profile = await _unitOfWork.Repository<PricingProfile>().GetByIdAsync(dto.PricingProfileId.Value);
        if (profile == null || profile.BranchId != branchId)
            throw new AppException("Invalid or inaccessible Pricing Profile.");

        var pc = new Pc
        {
            BranchId = branchId,
            PcNumber = dto.PcNumber,
            PcName = dto.PcName,
            Zone = dto.Zone,
            PricingProfileId = dto.PricingProfileId,
            HardwareNotes = dto.HardwareNotes,
            MonitorHz = dto.MonitorHz,
            State = PcState.Offline,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<Pc>().AddAsync(pc);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = superAdminId,
            UserRole = "SuperAdmin",
            UserName = "System",
            Action = "pc_added",
            BranchId = branchId,
            TargetType = "pc",
            TargetId = pc.Id,
            Details = new { PcNumber = pc.PcNumber, Zone = pc.Zone }
        });

        await _unitOfWork.CommitTransactionAsync();
        
        // Let's refetch to get PricingProfile name if applicable
        var created = await _unitOfWork.Repository<Pc>().Query().Include(p => p.PricingProfile).FirstOrDefaultAsync(p => p.Id == pc.Id);
        
        await _hubNotification.BroadcastPcManagementUpdateAsync(branchId, pc.Id, "added");
        return MapToDto(created!);
    }

    public async Task<PcDto> UpdatePcAsync(Guid pcId, Guid superAdminId, UpdatePcDto dto)
    {
        var pc = await _unitOfWork.Repository<Pc>().Query()
            .Include(p => p.PricingProfile)
            .FirstOrDefaultAsync(p => p.Id == pcId)
            ?? throw new NotFoundException("PC not found.");

        if (pc.IsDeleted)
            throw new AppException("Cannot update a deleted PC.");

        if (dto.PricingProfileId.HasValue && dto.PricingProfileId != pc.PricingProfileId)
        {
            var profile = await _unitOfWork.Repository<PricingProfile>().GetByIdAsync(dto.PricingProfileId.Value);
            if (profile == null || profile.BranchId != pc.BranchId)
                throw new AppException("Invalid or inaccessible Pricing Profile.");
        }

        pc.PcNumber = dto.PcNumber;
        pc.PcName = dto.PcName;
        pc.Zone = dto.Zone;
        pc.PricingProfileId = dto.PricingProfileId;
        pc.HardwareNotes = dto.HardwareNotes;
        pc.MonitorHz = dto.MonitorHz;
        pc.IsActive = dto.IsActive;
        pc.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<Pc>().Update(pc);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = superAdminId,
            UserRole = "SuperAdmin",
            UserName = "System",
            Action = "pc_updated",
            BranchId = pc.BranchId,
            TargetType = "pc",
            TargetId = pc.Id,
            Details = new { PcNumber = pc.PcNumber, IsActive = pc.IsActive }
        });

        await _unitOfWork.CommitTransactionAsync();
        
        var updated = await _unitOfWork.Repository<Pc>().Query().Include(p => p.PricingProfile).FirstOrDefaultAsync(p => p.Id == pc.Id);
        await _hubNotification.BroadcastPcManagementUpdateAsync(pc.BranchId, pc.Id, "updated");
        return MapToDto(updated!);
    }

    public async Task<PcDto> TransferPcAsync(Guid pcId, Guid newBranchId, Guid superAdminId)
    {
        var pc = await _unitOfWork.Repository<Pc>().Query()
            .FirstOrDefaultAsync(p => p.Id == pcId)
            ?? throw new NotFoundException("PC not found.");

        if (pc.IsDeleted)
            throw new AppException("Cannot transfer a deleted PC.");

        // Q3 Rule: Block PC transfers if active session, awaiting billing, or active reservation.
        if (pc.State == PcState.Active || pc.State == PcState.Reserved || pc.State == PcState.AwaitingBilling)
        {
            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = superAdminId,
                UserRole = "SuperAdmin",
                UserName = "System",
                Action = "pc_transfer_blocked",
                BranchId = pc.BranchId,
                TargetType = "pc",
                TargetId = pc.Id,
                Details = new { Reason = $"PC is in {pc.State} state" }
            });
            await _unitOfWork.CommitTransactionAsync(); // commit the audit
            throw new AppException($"Cannot transfer PC because it is in '{pc.State}' state. Please manually resolve the session first.");
        }

        var newBranch = await _unitOfWork.Repository<Branch>().GetByIdAsync(newBranchId)
            ?? throw new NotFoundException("Target branch not found.");

        var oldBranchId = pc.BranchId;
        pc.BranchId = newBranchId;
        
        // Remove pricing profile since it's branch-specific
        pc.PricingProfileId = null;
        pc.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<Pc>().Update(pc);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = superAdminId,
            UserRole = "SuperAdmin",
            UserName = "System",
            Action = "pc_transferred",
            BranchId = newBranchId,
            TargetType = "pc",
            TargetId = pc.Id,
            Details = new { OldBranch = oldBranchId, NewBranch = newBranchId }
        });

        await _unitOfWork.CommitTransactionAsync();
        
        // Notify both branches
        await _hubNotification.BroadcastPcManagementUpdateAsync(oldBranchId, pc.Id, "removed");
        await _hubNotification.BroadcastPcManagementUpdateAsync(newBranchId, pc.Id, "added");
        
        var transferred = await _unitOfWork.Repository<Pc>().Query().Include(p => p.PricingProfile).FirstOrDefaultAsync(p => p.Id == pc.Id);
        return MapToDto(transferred!);
    }

    public async Task<PcDto> MarkMaintenanceAsync(Guid pcId, Guid superAdminId, bool isMaintenance)
    {
        var pc = await _unitOfWork.Repository<Pc>().Query()
            .FirstOrDefaultAsync(p => p.Id == pcId)
            ?? throw new NotFoundException("PC not found.");

        if (pc.IsDeleted)
            throw new AppException("Cannot modify a deleted PC.");

        if (isMaintenance)
        {
            if (pc.State == PcState.Active || pc.State == PcState.Reserved || pc.State == PcState.AwaitingBilling)
                throw new AppException("Cannot place PC under maintenance while it is actively used or reserved.");
                
            pc.State = PcState.UnderMaintenance;
        }
        else
        {
            if (pc.State == PcState.UnderMaintenance || pc.State == PcState.Offline)
                pc.State = PcState.Idle;
        }
        
        pc.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<Pc>().Update(pc);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = superAdminId,
            UserRole = "SuperAdmin",
            UserName = "System",
            Action = isMaintenance ? "pc_maintenance_enabled" : "pc_maintenance_disabled",
            BranchId = pc.BranchId,
            TargetType = "pc",
            TargetId = pc.Id,
            Details = null
        });

        await _unitOfWork.CommitTransactionAsync();
        
        var updated = await _unitOfWork.Repository<Pc>().Query().Include(p => p.PricingProfile).FirstOrDefaultAsync(p => p.Id == pc.Id);
        await _hubNotification.BroadcastPcManagementUpdateAsync(pc.BranchId, pc.Id, isMaintenance ? "maintenance_enabled" : "maintenance_disabled");
        return MapToDto(updated!);
    }

    public async Task DeletePcAsync(Guid pcId, Guid superAdminId)
    {
        var pc = await _unitOfWork.Repository<Pc>().Query()
            .FirstOrDefaultAsync(p => p.Id == pcId)
            ?? throw new NotFoundException("PC not found.");

        if (pc.IsDeleted)
            return;

        if (pc.State == PcState.Active || pc.State == PcState.Reserved || pc.State == PcState.AwaitingBilling)
            throw new AppException("Cannot delete PC while it is actively used or reserved.");

        pc.IsDeleted = true;
        pc.IsActive = false;
        pc.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<Pc>().Update(pc);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = superAdminId,
            UserRole = "SuperAdmin",
            UserName = "System",
            Action = "pc_deleted",
            BranchId = pc.BranchId,
            TargetType = "pc",
            TargetId = pc.Id,
            Details = new { SoftDelete = true }
        });

        await _unitOfWork.CommitTransactionAsync();
        await _hubNotification.BroadcastPcManagementUpdateAsync(pc.BranchId, pc.Id, "removed");
    }

    private static PcDto MapToDto(Pc p)
    {
        return new PcDto
        {
            Id = p.Id,
            PcNumber = p.PcNumber,
            PcName = p.PcName,
            Zone = p.Zone,
            BranchId = p.BranchId,
            State = p.State,
            PricingProfileId = p.PricingProfileId,
            PricingProfileName = p.PricingProfile?.Name,
            HardwareNotes = p.HardwareNotes,
            MonitorHz = p.MonitorHz,
            IsActive = p.IsActive,
            IsDeleted = p.IsDeleted
        };
    }
}
