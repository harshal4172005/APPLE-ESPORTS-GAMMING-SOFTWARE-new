using AppleEsportsErp.Application.DTOs.Auth;

namespace AppleEsportsErp.Application.Interfaces;

/// <summary>
/// Authentication service contract — maps from auth.service.js
/// SOP §6: Login System, §21: Security Model, §10: Shift Management
/// </summary>
public interface IAuthService
{
    /// <summary>SOP §6.2: Super Admin Login</summary>
    Task<LoginResponseDto> LoginAdminAsync(AdminLoginDto dto);

    /// <summary>SOP §6.3: Operator Login — branch → profile → PIN → shift start</summary>
    Task<LoginResponseDto> LoginOperatorAsync(OperatorLoginDto dto);

    /// <summary>Member Login</summary>
    Task<LoginResponseDto> LoginMemberAsync(MemberLoginDto dto);

    /// <summary>SOP §10: Logout — closes shift for operators</summary>
    Task LogoutAsync(Guid userId, string role, Guid? shiftId);

    /// <summary>SOP §11: Force Logout — Super Admin forcibly logs out an operator</summary>
    Task<ForceLogoutResponseDto> ForceLogoutAsync(Guid adminId, Guid operatorId);

    /// <summary>SOP §22: Get available admins for Quick-Switch</summary>
    Task<IEnumerable<AvailableAdminDto>> GetAvailableAdminsForSwitchAsync();

    /// <summary>SOP §22: Admin Quick-Switch In</summary>
    Task<LoginResponseDto> AdminSwitchInAsync(AdminSwitchInDto dto);

    /// <summary>SOP §22: Admin Quick-Switch Out</summary>
    Task AdminSwitchOutAsync(Guid adminId, Guid shiftId);

    /// <summary>Refresh access token</summary>
    Task<TokenResponseDto> RefreshAccessTokenAsync(string refreshToken);

    /// <summary>SOP §19: Current user profile with dashboard permissions</summary>
    Task<UserProfileDto> GetCurrentUserAsync(Guid userId, string role);

    /// <summary>SOP §6.3 Step 2: Get active branches for login screen</summary>
    Task<IEnumerable<BranchListItemDto>> GetActiveBranchesAsync();

    /// <summary>SOP §6.3 Step 3: Get operators for a branch</summary>
    Task<IEnumerable<OperatorListItemDto>> GetBranchOperatorsAsync(Guid branchId);

    /// <summary>Verify if admin password is valid</summary>
    Task<bool> VerifyAdminPasswordAsync(string password);

    /// <summary>
    /// Generate a 30-day emergency offline JWT for the given user.
    /// Embeds role, branchId, and dashboardPermissions so the client can operate offline.
    /// </summary>
    Task<string> GenerateEmergencyTokenAsync(Guid userId, string role, string? branchId, string? dashboardPermissions);

    // Initial Setup Methods
    Task<CheckSetupResponseDto> CheckSetupStatusAsync();
    Task<LoginResponseDto> SetupMasterAccountAsync(SetupMasterDto dto);
    Task<LoginResponseDto> SetupOperatorAccountAsync(SetupOperatorDto dto);

    // Password Reset Methods
    Task InitiatePasswordResetAsync(string email);
    Task CompletePasswordResetAsync(ResetPasswordDto dto);
    Task ChangeCredentialsAsync(Guid userId, string role, ChangeCredentialsDto dto);
}
