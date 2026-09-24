using AppleEsportsErp.Application.DTOs.Settings;

namespace AppleEsportsErp.Infrastructure.Services;

public interface IPricingProfileService
{
    Task<IEnumerable<PricingProfileDto>> GetAllByBranchAsync(Guid branchId);
    Task<PricingProfileDto> CreateAsync(CreatePricingProfileDto dto);
    Task<PricingProfileDto> UpdateAsync(Guid id, UpdatePricingProfileDto dto);
    Task DeleteAsync(Guid id);

    Task<PricingPackageDto> CreatePackageAsync(CreatePricingPackageDto dto);
    Task<PricingPackageDto> UpdatePackageAsync(Guid id, UpdatePricingPackageDto dto);
    Task DeletePackageAsync(Guid id);
}
