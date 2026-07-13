using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.Auth;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.Constants;

namespace AppleEsportsErp.Api.Controllers;

/// <summary>
/// Authentication controller — maps from auth.routes.js + auth.controller.js.
/// SOP §6: Login System (Admin + Operator flows)
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>SOP §6.2: Super Admin Login — POST /api/auth/admin/login</summary>
    [HttpPost("admin/login")]
    [AllowAnonymous]
    public async Task<IActionResult> AdminLogin([FromBody] AdminLoginDto dto)
    {
        var result = await _authService.LoginAdminAsync(dto);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    /// <summary>SOP §6.3: Operator Login — POST /api/auth/operator/login</summary>
    [HttpPost("operator/login")]
    [AllowAnonymous]
    public async Task<IActionResult> OperatorLogin([FromBody] OperatorLoginDto dto)
    {
        var result = await _authService.LoginOperatorAsync(dto);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    /// <summary>Member Login — POST /api/auth/member/login</summary>
    [HttpPost("member/login")]
    [AllowAnonymous]
    public async Task<IActionResult> MemberLogin([FromBody] MemberLoginDto dto)
    {
        var result = await _authService.LoginMemberAsync(dto);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    /// <summary>SOP §10: Logout — POST /api/auth/logout</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role)!;
        var shiftIdClaim = User.FindFirstValue("shiftId");
        var shiftId = string.IsNullOrEmpty(shiftIdClaim) ? (Guid?)null : Guid.Parse(shiftIdClaim);

        await _authService.LogoutAsync(userId, role, shiftId);
        return Ok(ApiResponse.Ok());
    }

    /// <summary>Refresh token — POST /api/auth/refresh</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        var result = await _authService.RefreshAccessTokenAsync(dto.RefreshToken);
        return Ok(ApiResponse<TokenResponseDto>.Ok(result));
    }

    /// <summary>SOP §19: Get current user — GET /api/auth/me</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role)!;

        var result = await _authService.GetCurrentUserAsync(userId, role);
        return Ok(ApiResponse<UserProfileDto>.Ok(result));
    }

    /// <summary>SOP §6.3 Step 2: Get branches — GET /api/auth/branches</summary>
    [HttpGet("branches")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBranches()
    {
        var branches = await _authService.GetActiveBranchesAsync();
        return Ok(ApiResponse<IEnumerable<BranchListItemDto>>.Ok(branches));
    }

    /// <summary>SOP §6.3 Step 3: Get operators — GET /api/auth/operators/{branchId}</summary>
    [HttpGet("operators/{branchId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBranchOperators(Guid branchId)
    {
        var operators = await _authService.GetBranchOperatorsAsync(branchId);
        return Ok(ApiResponse<IEnumerable<OperatorListItemDto>>.Ok(operators));
    }

    /// <summary>SOP §11: Force logout — POST /api/auth/force-logout/{id}</summary>
    [HttpPost("force-logout/{id:guid}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> ForceLogout(Guid id)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _authService.ForceLogoutAsync(adminId, id);
        return Ok(ApiResponse<ForceLogoutResponseDto>.Ok(result));
    }

    [HttpGet("check-setup")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckSetupStatus()
    {
        var result = await _authService.CheckSetupStatusAsync();
        return Ok(ApiResponse<CheckSetupResponseDto>.Ok(result));
    }

    [HttpPost("setup-master")]
    [AllowAnonymous]
    public async Task<IActionResult> SetupMasterAccount([FromBody] SetupMasterDto dto)
    {
        var result = await _authService.SetupMasterAccountAsync(dto);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    [HttpPost("setup-operator")]
    [AllowAnonymous]
    public async Task<IActionResult> SetupOperatorAccount([FromBody] SetupOperatorDto dto)
    {
        var result = await _authService.SetupOperatorAccountAsync(dto);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        await _authService.InitiatePasswordResetAsync(dto.Email);
        return Ok(ApiResponse<object>.Ok(new { message = "If that email exists, a reset link has been sent." }));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        await _authService.CompletePasswordResetAsync(dto);
        return Ok(ApiResponse<object>.Ok(new { message = "Password reset successfully." }));
    }

    [HttpPost("change-credentials")]
    [Authorize]
    public async Task<IActionResult> ChangeCredentials([FromBody] ChangeCredentialsDto dto)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role)!;
        await _authService.ChangeCredentialsAsync(userId, role, dto);
        return Ok(ApiResponse<object>.Ok(new { message = "Credentials updated successfully." }));
    }

    /// <summary>Verify admin password — POST /api/auth/verify-admin</summary>
    [HttpPost("verify-admin")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyAdmin([FromBody] VerifyAdminDto dto)
    {
        var isValid = await _authService.VerifyAdminPasswordAsync(dto.Password);
        if (!isValid)
        {
            return BadRequest(ApiResponse.Fail("Invalid admin password", "INVALID_ADMIN_PASSWORD"));
        }
        return Ok(ApiResponse.Ok());
    }

    /// <summary>
    /// Emergency Offline JWT Generation — POST /api/auth/emergency-token.
    /// Returns a 30-day signed JWT embedding the caller's role, branchId, and dashboard permissions.
    /// The client stores this in IndexedDB behind a 4-digit PIN for offline operation.
    /// </summary>
    [HttpPost("emergency-token")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> GenerateEmergencyToken()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role)!;
        var branchId = User.FindFirstValue("branchId");
        var dashboardPermissions = User.FindFirstValue("dashboardPermissions");

        var token = await _authService.GenerateEmergencyTokenAsync(userId, role, branchId, dashboardPermissions);
        return Ok(new { token });
    }

    /// <summary>SOP §22: Admin Quick-Switch Available</summary>
    [HttpGet("admin-switch/available")]
    [Authorize(Roles = Roles.Operator)]
    public async Task<IActionResult> GetAvailableAdminsForSwitch()
    {
        var result = await _authService.GetAvailableAdminsForSwitchAsync();
        return Ok(ApiResponse<IEnumerable<AvailableAdminDto>>.Ok(result));
    }

    /// <summary>SOP §22: Admin Quick-Switch In</summary>
    [HttpPost("admin-switch/in")]
    [Authorize(Roles = Roles.Operator)]
    public async Task<IActionResult> AdminSwitchIn([FromBody] AdminSwitchInDto dto)
    {
        // Require that the request is coming from an authenticated Operator
        var shiftIdClaim = User.FindFirstValue("shiftId");
        if (string.IsNullOrEmpty(shiftIdClaim))
            return Unauthorized(ApiResponse.Fail("Must be inside an active shift."));

        dto.ShiftId = Guid.Parse(shiftIdClaim);
        var result = await _authService.AdminSwitchInAsync(dto);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    /// <summary>SOP §22: Admin Quick-Switch Out</summary>
    [HttpPost("admin-switch/out")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminSwitchOut()
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var shiftIdClaim = User.FindFirstValue("shiftId");
        
        // Ensure this is actually a switched-in token
        var isSwitchedAdmin = User.FindFirstValue("isSwitchedAdmin");
        if (isSwitchedAdmin != "true")
            return BadRequest(ApiResponse.Fail("Token is not an admin switch token."));

        if (!string.IsNullOrEmpty(shiftIdClaim))
        {
            await _authService.AdminSwitchOutAsync(adminId, Guid.Parse(shiftIdClaim));
        }

        return Ok(ApiResponse.Ok());
    }
}


