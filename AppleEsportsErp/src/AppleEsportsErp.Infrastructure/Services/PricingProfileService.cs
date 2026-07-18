using AppleEsportsErp.Application.DTOs.Settings;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Infrastructure.Data;
using AppleEsportsErp.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AppleEsportsErp.Infrastructure.Services;

public class PricingProfileService : IPricingProfileService
{
    private readonly AppDbContext _db;
    private readonly IHubNotificationService _hubNotifier;

    public PricingProfileService(AppDbContext db, IHubNotificationService hubNotifier)
    {
        _db = db;
        _hubNotifier = hubNotifier;
    }

    public async Task<IEnumerable<PricingProfileDto>> GetAllByBranchAsync(Guid branchId)
    {
        var profiles = await _db.PricingProfiles
            .Where(p => p.BranchId == branchId && p.IsActive)
            .OrderBy(p => p.BaseHourlyRate)
            .ToListAsync();

        return profiles.Select(p => new PricingProfileDto
        {
            Id = p.Id,
            Name = p.Name,
            BaseHourlyRate = p.BaseHourlyRate,
            BufferMinutes = p.BufferMinutes,
            BranchId = p.BranchId,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            RefreshRate = p.RefreshRate,
            SystemSpecs = p.SystemSpecs
        });
    }

    public async Task<PricingProfileDto> CreateAsync(CreatePricingProfileDto dto)
    {
        var profile = new PricingProfile
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            BaseHourlyRate = dto.BaseHourlyRate,
            BufferMinutes = dto.BufferMinutes,
            BranchId = dto.BranchId,
            IsActive = dto.IsActive,
            RefreshRate = dto.RefreshRate,
            SystemSpecs = dto.SystemSpecs,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.PricingProfiles.Add(profile);
        await _db.SaveChangesAsync();
        await _hubNotifier.BroadcastPricingProfileUpdateAsync(profile.BranchId);

        return new PricingProfileDto
        {
            Id = profile.Id,
            Name = profile.Name,
            BaseHourlyRate = profile.BaseHourlyRate,
            BufferMinutes = profile.BufferMinutes,
            BranchId = profile.BranchId,
            IsActive = profile.IsActive,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            RefreshRate = profile.RefreshRate,
            SystemSpecs = profile.SystemSpecs
        };
    }

    public async Task<PricingProfileDto> UpdateAsync(Guid id, UpdatePricingProfileDto dto)
    {
        var profile = await _db.PricingProfiles.FindAsync(id)
            ?? throw new NotFoundException("Pricing profile not found.");

        profile.Name = dto.Name;
        profile.BaseHourlyRate = dto.BaseHourlyRate;
        profile.BufferMinutes = dto.BufferMinutes;
        profile.IsActive = dto.IsActive;
        profile.RefreshRate = dto.RefreshRate;
        profile.SystemSpecs = dto.SystemSpecs;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        await _hubNotifier.BroadcastPricingProfileUpdateAsync(profile.BranchId);

        return new PricingProfileDto
        {
            Id = profile.Id,
            Name = profile.Name,
            BaseHourlyRate = profile.BaseHourlyRate,
            BufferMinutes = profile.BufferMinutes,
            BranchId = profile.BranchId,
            IsActive = profile.IsActive,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            RefreshRate = profile.RefreshRate,
            SystemSpecs = profile.SystemSpecs
        };
    }

    public async Task DeleteAsync(Guid id)
    {
        var profile = await _db.PricingProfiles.FindAsync(id)
            ?? throw new NotFoundException("Pricing profile not found.");

        // Soft delete so historical session data isn't affected
        profile.IsActive = false;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        await _hubNotifier.BroadcastPricingProfileUpdateAsync(profile.BranchId);
    }
}
