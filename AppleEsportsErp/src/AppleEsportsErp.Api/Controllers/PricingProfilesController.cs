using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Application.DTOs.Settings;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Infrastructure.Services;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Application.Exceptions;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/pricing-profiles")]
[Authorize(Policy = "Dashboard:settings")]
public class PricingProfilesController : ControllerBase
{
    private readonly IPricingProfileService _pricingProfileService;

    public PricingProfilesController(IPricingProfileService pricingProfileService)
    {
        _pricingProfileService = pricingProfileService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllByBranch([FromQuery] Guid branchId)
    {
        if (branchId == Guid.Empty)
            return BadRequest(ApiResponse<object>.Fail("BranchId is required."));

        var profiles = await _pricingProfileService.GetAllByBranchAsync(branchId);
        return Ok(ApiResponse<IEnumerable<PricingProfileDto>>.Ok(profiles));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePricingProfileDto dto)
    {
        try
        {
            var profile = await _pricingProfileService.CreateAsync(dto);
            return Ok(ApiResponse<PricingProfileDto>.Ok(profile));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePricingProfileDto dto)
    {
        try
        {
            var profile = await _pricingProfileService.UpdateAsync(id, dto);
            return Ok(ApiResponse<PricingProfileDto>.Ok(profile));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _pricingProfileService.DeleteAsync(id);
            return Ok(ApiResponse<object>.Ok(null));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
