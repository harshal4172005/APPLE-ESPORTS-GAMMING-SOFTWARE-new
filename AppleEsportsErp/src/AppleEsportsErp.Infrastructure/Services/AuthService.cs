using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Auth;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;
using AppleEsportsErp.Infrastructure.Identity;
using BCryptNet = BCrypt.Net.BCrypt;

namespace AppleEsportsErp.Infrastructure.Services;

/// <summary>
/// Full AuthService implementation — 1:1 mapping from auth.service.js.
/// SOP §6: Login System (Admin + Operator flows)
/// SOP §21: Security Model (hashing, tokens, device tracking)
/// SOP §10: Shift Management (auto shift start on login)
/// </summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly IAuditService _audit;
    private readonly ILogger<AuthService> _logger;
    private readonly ITokenRevocationService _tokenRevocation;
    private readonly IEmailService _emailService;
    private const int SALT_ROUNDS = 12;

    public AuthService(AppDbContext db, JwtTokenService jwt, IAuditService audit, ILogger<AuthService> logger, ITokenRevocationService tokenRevocation, IEmailService emailService)
    {
        _db = db;
        _jwt = jwt;
        _audit = audit;
        _logger = logger;
        _tokenRevocation = tokenRevocation;
        _emailService = emailService;
    }

    /// <summary>
    /// SOP §6.2: Super Admin Login — Email/Password → Validate credentials, permissions, device, account status.
    /// SOP: Super Admin session persists until logout/timeout/password reset/forced signout.
    /// Maps from: auth.service.js loginAdmin()
    /// </summary>
    public async Task<LoginResponseDto> LoginAdminAsync(AdminLoginDto dto)
    {
        // 1. Find admin user
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user != null)
        {
            // 2. Check account status
            if (user.Status != UserStatus.Active)
                throw new AuthorizationException($"Account is {user.Status}. Contact system administrator.", "ACCOUNT_INACTIVE");

            // 3. Verify password — SOP §21.1: Password Hashing = YES
            if (!BCryptNet.Verify(dto.Password, user.PasswordHash))
            {
                // Log failed attempt — SOP §22: Audit every critical action
                await _audit.LogAsync(new AuditEntry
                {
                    UserId = user.Id,
                    UserRole = Roles.SuperAdmin,
                    UserName = user.FullName,
                    Action = AuditActions.FailedLogin,
                    Details = new { reason = "Invalid password", deviceInfo = dto.DeviceInfo },
                });
                throw new AuthenticationException("Invalid email/username or password", "INVALID_CREDENTIALS");
            }

            // 4. Generate tokens — Q1 Decision: full claims embedded in JWT
            var claims = new Dictionary<string, string>
            {
                [ClaimTypes.NameIdentifier] = user.Id.ToString(),
                [ClaimTypes.Role] = user.Role,
                [ClaimTypes.Name] = user.FullName,
            };

            if (user.Role == Roles.Admin && !string.IsNullOrEmpty(user.DashboardPermissions))
            {
                claims["dashboardPermissions"] = user.DashboardPermissions;
            }

            var accessToken = _jwt.GenerateAccessToken(claims);
            var refreshToken = _jwt.GenerateRefreshToken(claims);

            // 5. Update last login + device info — SOP §21.1: Device Tracking
            user.LastLogin = DateTimeOffset.UtcNow;
            user.DeviceInfo = dto.DeviceInfo != null ? JsonSerializer.Serialize(dto.DeviceInfo) : null;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            // 6. Audit log — SOP §22
            await _audit.LogAsync(new AuditEntry
            {
                UserId = user.Id,
                UserRole = user.Role,
                UserName = user.FullName,
                Action = AuditActions.Login,
                Details = new { method = "email_password", deviceInfo = dto.DeviceInfo },
            });

            _logger.LogInformation("{Role} logged in: {Name}", user.Role, user.FullName);

            return new LoginResponseDto
            {
                User = new UserProfileDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role,
                    DashboardPermissions = user.DashboardPermissions != null 
                        ? JsonSerializer.Deserialize<object>(user.DashboardPermissions) 
                        : null,
                    Status = user.Status.ToString().ToLowerInvariant(),
                    LastLogin = user.LastLogin,
                },
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            };
        }

        // 1b. Fallback: check if it's an Operator promoted to Global Admin
        var op = await _db.Operators.FirstOrDefaultAsync(o => o.Username == dto.Email && o.IsGlobalAdmin);
        if (op != null)
        {
            if (op.Status != OperatorStatus.Active)
                throw new AuthorizationException($"Account is {op.Status}. Contact system administrator.", "ACCOUNT_INACTIVE");

            if (!BCryptNet.Verify(dto.Password, op.PasswordHash))
            {
                await _audit.LogAsync(new AuditEntry
                {
                    OperatorId = op.Id,
                    UserRole = Roles.Admin,
                    UserName = op.FullName,
                    Action = AuditActions.FailedLogin,
                    Details = new { reason = "Invalid password", deviceInfo = dto.DeviceInfo },
                });
                throw new AuthenticationException("Invalid email/username or password", "INVALID_CREDENTIALS");
            }

            var claims = new Dictionary<string, string>
            {
                [ClaimTypes.NameIdentifier] = op.Id.ToString(),
                [ClaimTypes.Role] = Roles.Admin,
                [ClaimTypes.Name] = op.FullName,
            };

            if (!string.IsNullOrEmpty(op.DashboardPermissions))
            {
                claims["dashboardPermissions"] = op.DashboardPermissions;
            }

            var accessToken = _jwt.GenerateAccessToken(claims);
            var refreshToken = _jwt.GenerateRefreshToken(claims);

            op.LastLogin = DateTimeOffset.UtcNow;
            op.DeviceInfo = dto.DeviceInfo != null ? JsonSerializer.Serialize(dto.DeviceInfo) : null;
            op.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            await _audit.LogAsync(new AuditEntry
            {
                OperatorId = op.Id,
                UserRole = Roles.Admin,
                UserName = op.FullName,
                Action = AuditActions.Login,
                Details = new { method = "username_password_admin_fallback", deviceInfo = dto.DeviceInfo },
            });

            _logger.LogInformation("{Role} logged in: {Name} (Operator Fallback)", Roles.Admin, op.FullName);

            return new LoginResponseDto
            {
                User = new UserProfileDto
                {
                    Id = op.Id,
                    Email = op.Username, // Map username to email field for frontend compatibility
                    FullName = op.FullName,
                    Role = Roles.Admin,
                    DashboardPermissions = op.DashboardPermissions != null 
                        ? JsonSerializer.Deserialize<object>(op.DashboardPermissions) 
                        : null,
                    Status = op.Status.ToString().ToLowerInvariant(),
                    LastLogin = op.LastLogin,
                },
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            };
        }

        throw new AuthenticationException("Invalid email/username or password", "INVALID_CREDENTIALS");
    }

    /// <summary>
    /// SOP §6.3: Operator Login — Select Branch → Select Profile → Enter PIN → System starts shift.
    /// SOP: Operator CANNOT see other branch data (enforced via branch assignment check).
    /// Maps from: auth.service.js loginOperator()
    /// </summary>
    public async Task<LoginResponseDto> LoginOperatorAsync(OperatorLoginDto dto)
    {
        // 1. Verify branch exists and is active
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == dto.BranchId);
        if (branch == null)
            throw new NotFoundException("Branch not found", "BRANCH_NOT_FOUND");
        if (branch.Status != BranchStatus.Active)
            throw new AuthorizationException("Branch is currently inactive", "BRANCH_INACTIVE");

        // 2. Find operator assigned to this branch
        var op = await _db.Operators.FirstOrDefaultAsync(
            o => o.Username == dto.Username && o.BranchId == dto.BranchId);
        if (op == null)
            throw new AuthenticationException("Invalid credentials or operator not assigned to this branch", "INVALID_CREDENTIALS");

        // 3. Check operator status — SOP §12: Operator Status Types
        if (op.Status == OperatorStatus.Suspended)
            throw new AuthorizationException("Operator account is suspended. Contact Super Admin.", "OPERATOR_SUSPENDED");
        if (op.Status == OperatorStatus.Disabled)
            throw new AuthorizationException("Operator account is disabled. Contact Super Admin.", "OPERATOR_DISABLED");

        // 4. Verify password/PIN
        if (!BCryptNet.Verify(dto.Password, op.PasswordHash))
        {
            await _audit.LogAsync(new AuditEntry
            {
                OperatorId = op.Id,
                UserRole = Roles.Operator,
                UserName = op.FullName,
                Action = AuditActions.FailedLogin,
                BranchId = dto.BranchId,
                BranchName = branch.Name,
                Details = new { reason = "Invalid password/PIN", deviceInfo = dto.DeviceInfo },
            });
            throw new AuthenticationException("Invalid password or PIN", "INVALID_CREDENTIALS");
        }

        // 5. Start shift — SOP §5: log operator, branch, login time, device
        var shift = new Shift
        {
            OperatorId = op.Id,
            BranchId = dto.BranchId,
            LoginTime = DateTimeOffset.UtcNow,
            DeviceInfo = dto.DeviceInfo != null ? JsonSerializer.Serialize(dto.DeviceInfo) : null,
            Status = ShiftStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Shifts.Add(shift);

        // 6. Update operator status to active
        op.Status = OperatorStatus.Active;
        op.LastLogin = DateTimeOffset.UtcNow;
        op.DeviceInfo = dto.DeviceInfo != null ? JsonSerializer.Serialize(dto.DeviceInfo) : null;
        op.IsOnline = true;
        op.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        // 7. Generate tokens with branch + permissions embedded — Q1 Decision
        var claims = new Dictionary<string, string>
        {
            [ClaimTypes.NameIdentifier] = op.Id.ToString(),
            [ClaimTypes.Role] = Roles.Operator,
            [ClaimTypes.Name] = op.FullName,
            ["branchId"] = op.BranchId.ToString(),
            ["shiftId"] = shift.Id.ToString(),
            ["dashboardPermissions"] = op.DashboardPermissions ?? "{}",
        };

        var accessToken = _jwt.GenerateAccessToken(claims);
        var refreshToken = _jwt.GenerateRefreshToken(claims);

        // 8. Audit log — login
        await _audit.LogAsync(new AuditEntry
        {
            OperatorId = op.Id,
            UserRole = Roles.Operator,
            UserName = op.FullName,
            Action = AuditActions.Login,
            BranchId = dto.BranchId,
            BranchName = branch.Name,
            Details = new { shiftId = shift.Id, loginTime = shift.LoginTime, deviceInfo = dto.DeviceInfo },
        });

        // 9. Shift start audit
        await _audit.LogAsync(new AuditEntry
        {
            OperatorId = op.Id,
            UserRole = Roles.Operator,
            UserName = op.FullName,
            Action = AuditActions.ShiftStart,
            BranchId = dto.BranchId,
            BranchName = branch.Name,
            Details = new { shiftId = shift.Id },
        });

        _logger.LogInformation("Operator logged in: {Name} @ {Branch}", op.FullName, branch.Name);

        return new LoginResponseDto
        {
            User = new UserProfileDto
            {
                Id = op.Id,
                FullName = op.FullName,
                Username = op.Username,
                Role = Roles.Operator,
                BranchId = op.BranchId,
                BranchName = branch.Name,
                ShiftId = shift.Id,
                DashboardPermissions = op.DashboardPermissions != null
                    ? JsonSerializer.Deserialize<object>(op.DashboardPermissions)
                    : null,
                Status = op.Status.ToString().ToLowerInvariant(),
                LastLogin = op.LastLogin,
                ActiveShift = new ActiveShiftDto { Id = shift.Id, LoginTime = shift.LoginTime },
            },
            AccessToken = accessToken,
            RefreshToken = refreshToken,
        };
    }

    /// <summary>
    /// Member Login - Accessible via PC Client Overlay across any branch.
    /// Uses Username, MobileNumber or Email + Password.
    /// </summary>
    public async Task<LoginResponseDto> LoginMemberAsync(MemberLoginDto dto)
    {
        // Find member by Username, MobileNumber, or Email
        var member = await _db.Members.FirstOrDefaultAsync(m => 
            (m.Username != null && m.Username == dto.Identifier) || 
            (m.MobileNumber != null && m.MobileNumber == dto.Identifier) || 
            (m.Email != null && m.Email == dto.Identifier));

        if (member == null)
            throw new AuthenticationException("Invalid credentials", "INVALID_CREDENTIALS");

        if (member.Status != MemberStatus.Active)
            throw new AuthorizationException($"Account is {member.Status}. Please contact front desk.", "ACCOUNT_INACTIVE");

        if (string.IsNullOrEmpty(member.PasswordHash) || !BCryptNet.Verify(dto.Password, member.PasswordHash))
        {
            await _audit.LogAsync(new AuditEntry
            {
                UserRole = "Member",
                UserName = member.FullName,
                Action = AuditActions.FailedLogin,
                Details = new { reason = "Invalid password", deviceInfo = dto.DeviceInfo },
            });
            throw new AuthenticationException("Invalid credentials", "INVALID_CREDENTIALS");
        }

        // Generate JWT
        var claims = new Dictionary<string, string>
        {
            [ClaimTypes.NameIdentifier] = member.Id.ToString(),
            [ClaimTypes.Role] = "Member",
            [ClaimTypes.Name] = member.FullName,
        };

        var accessToken = _jwt.GenerateAccessToken(claims);
        var refreshToken = _jwt.GenerateRefreshToken(claims);

        member.LastVisit = DateTimeOffset.UtcNow;
        member.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(new AuditEntry
        {
            UserRole = "Member",
            UserName = member.FullName,
            Action = AuditActions.Login,
            Details = new { deviceInfo = dto.DeviceInfo },
        });

        _logger.LogInformation("Member logged in: {Name}", member.FullName);

        return new LoginResponseDto
        {
            User = new UserProfileDto
            {
                Id = member.Id,
                Username = member.Username,
                FullName = member.FullName,
                Role = "Member",
                Status = member.Status.ToString().ToLowerInvariant(),
                LastLogin = member.LastVisit,
            },
            AccessToken = accessToken,
            RefreshToken = refreshToken,
        };
    }

    /// <summary>
    /// SOP §10: Logout — closes shift for operators.
    /// SOP: System records logout time, shift summary, revenue, actions.
    /// Maps from: auth.service.js logout()
    /// </summary>
    public async Task LogoutAsync(Guid userId, string role, Guid? shiftId)
    {
        if (role == Roles.Operator && shiftId.HasValue)
        {
            // Close the operator's shift
            var shift = await _db.Shifts.FirstOrDefaultAsync(
                s => s.Id == shiftId.Value && s.OperatorId == userId && s.Status == ShiftStatus.Active);
            if (shift != null)
            {
                shift.LogoutTime = DateTimeOffset.UtcNow;
                shift.Status = ShiftStatus.Completed;
            }

            // Update operator status
            var op = await _db.Operators.FindAsync(userId);
            if (op != null) 
            {
                op.Status = OperatorStatus.LoggedOut;
                op.IsOnline = false;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Operator shift ended: {UserId}, shift: {ShiftId}", userId, shiftId);
        }

        // Fetch user name for audit
        string userName = "Unknown";
        if (role == Roles.SuperAdmin)
        {
            var user = await _db.Users.FindAsync(userId);
            userName = user?.FullName ?? "Admin";
        }
        else
        {
            var op = await _db.Operators.FindAsync(userId);
            userName = op?.FullName ?? "Operator";
        }

        await _audit.LogAsync(new AuditEntry
        {
            UserId = role == Roles.SuperAdmin ? userId : null,
            OperatorId = role == Roles.Operator ? userId : null,
            UserRole = role,
            UserName = userName,
            Action = AuditActions.Logout,
            Details = new { shiftId },
        });

        // Revoke tokens globally for this user (hardens against stale token reuse)
        await _tokenRevocation.RevokeUserTokensAsync(userId, TimeSpan.FromDays(7));
    }

    /// <summary>
    /// SOP §11: Force Logout — Super Admin forcibly logs out an operator.
    /// Instantly revoke access, terminate session, block future login.
    /// Maps from: auth.service.js forceLogout()
    /// </summary>
    public async Task<ForceLogoutResponseDto> ForceLogoutAsync(Guid adminId, Guid operatorId)
    {
        var op = await _db.Operators
            .Include(o => o.Branch)
            .FirstOrDefaultAsync(o => o.Id == operatorId);
        if (op == null)
            throw new NotFoundException("Operator not found", "OPERATOR_NOT_FOUND");

        // Close ALL active shifts for this operator
        var activeShifts = await _db.Shifts
            .Where(s => s.OperatorId == operatorId && s.Status == ShiftStatus.Active)
            .ToListAsync();
        foreach (var shift in activeShifts)
        {
            shift.LogoutTime = DateTimeOffset.UtcNow;
            shift.Status = ShiftStatus.ForceClosed;
        }

        // Set operator status to logged_out
        op.Status = OperatorStatus.LoggedOut;
        op.IsOnline = false;

        await _db.SaveChangesAsync();

        // Get admin name for audit
        var admin = await _db.Users.FindAsync(adminId);
        var adminName = admin?.FullName ?? "Admin";

        await _audit.LogAsync(new AuditEntry
        {
            UserId = adminId,
            UserRole = Roles.SuperAdmin,
            UserName = adminName,
            Action = AuditActions.ForcedLogout,
            TargetType = "operator",
            TargetId = operatorId,
            BranchId = op.BranchId,
            BranchName = op.Branch?.Name,
            Details = new { operatorName = op.FullName, reason = "Forced logout by Super Admin" },
        });

        _logger.LogWarning("FORCE LOGOUT: {Operator} by {Admin}", op.FullName, adminName);

        // Force revoke all existing tokens for this operator
        await _tokenRevocation.RevokeUserTokensAsync(operatorId, TimeSpan.FromDays(7));

        return new ForceLogoutResponseDto { Success = true, Operator = op.FullName };
    }

    /// <summary>
    /// Refresh access token — re-verify user is still active before issuing.
    /// Maps from: auth.service.js refreshAccessToken()
    /// </summary>
    public async Task<TokenResponseDto> RefreshAccessTokenAsync(string refreshToken)
    {
        var principal = _jwt.ValidateRefreshToken(refreshToken);
        if (principal == null)
            throw new AuthenticationException("Invalid refresh token", "REFRESH_INVALID");

        var id = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var role = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(role))
            throw new AuthenticationException("Invalid refresh token", "REFRESH_INVALID");

        var userId = Guid.Parse(id);

        // Re-verify user still exists and is active
        if (role == Roles.SuperAdmin)
        {
            var active = await _db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active);
            if (!active) throw new AuthorizationException("Account is no longer active", "ACCOUNT_INACTIVE");
        }
        else if (role == Roles.Operator)
        {
            var active = await _db.Operators.AnyAsync(o => o.Id == userId && o.Status == OperatorStatus.Active);
            if (!active) throw new AuthorizationException("Account is no longer active", "ACCOUNT_INACTIVE");
        }

        // Filter out protocol claims (exp, iat, nbf, iss, aud, jti) to prevent duplicates and validation failures
        var protocolClaims = new System.Collections.Generic.HashSet<string>(new[] { 
            "exp", "iat", "nbf", "iss", "aud", "jti"
        });

        var claims = new Dictionary<string, string>();
        foreach (var claim in principal.Claims)
        {
            if (!protocolClaims.Contains(claim.Type) && !claims.ContainsKey(claim.Type))
                claims[claim.Type] = claim.Value;
        }

        return new TokenResponseDto { AccessToken = _jwt.GenerateAccessToken(claims) };
    }

    /// <summary>
    /// SOP §19: Get current user profile with permissions.
    /// Returns full dashboard permission map for frontend rendering.
    /// Maps from: auth.service.js getCurrentUser()
    /// </summary>
    public async Task<UserProfileDto> GetCurrentUserAsync(Guid userId, string role)
    {
        if (role == Roles.SuperAdmin || role == Roles.Admin)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) throw new NotFoundException("User not found", "USER_NOT_FOUND");

            return new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                DashboardPermissions = user.DashboardPermissions != null 
                    ? JsonSerializer.Deserialize<object>(user.DashboardPermissions) 
                    : null,
                Status = user.Status.ToString().ToLowerInvariant(),
                LastLogin = user.LastLogin,
            };
        }

        if (role == Roles.Operator)
        {
            var op = await _db.Operators
                .Include(o => o.Branch)
                .FirstOrDefaultAsync(o => o.Id == userId);
            if (op == null) throw new NotFoundException("Operator not found", "OPERATOR_NOT_FOUND");

            // Get active shift
            var activeShift = await _db.Shifts
                .Where(s => s.OperatorId == userId && s.Status == ShiftStatus.Active)
                .OrderByDescending(s => s.LoginTime)
                .Select(s => new ActiveShiftDto { Id = s.Id, LoginTime = s.LoginTime })
                .FirstOrDefaultAsync();

            return new UserProfileDto
            {
                Id = op.Id,
                FullName = op.FullName,
                Username = op.Username,
                Role = Roles.Operator,
                BranchId = op.BranchId,
                BranchName = op.Branch?.Name,
                DashboardPermissions = op.DashboardPermissions != null
                    ? JsonSerializer.Deserialize<object>(op.DashboardPermissions)
                    : null,
                Status = op.Status.ToString().ToLowerInvariant(),
                LastLogin = op.LastLogin,
                ActiveShift = activeShift,
            };
        }

        throw new AppException("Invalid role", System.Net.HttpStatusCode.BadRequest, "INVALID_ROLE");
    }

    /// <summary>SOP §6.3 Step 2: Get active branches for login screen</summary>
    public async Task<IEnumerable<BranchListItemDto>> GetActiveBranchesAsync()
    {
        return await _db.Branches
            .Where(b => b.Status == BranchStatus.Active)
            .OrderBy(b => b.Name)
            .Select(b => new BranchListItemDto
            {
                Id = b.Id,
                Name = b.Name,
                Address = b.Address,
                Status = b.Status.ToString().ToLowerInvariant(),
                OpeningTime = b.OpeningTime.ToString("HH:mm"),
                ClosingTime = b.ClosingTime.ToString("HH:mm"),
            })
            .ToListAsync();
    }

    /// <summary>SOP §6.3 Step 3: Get operators for a branch (for operator selection screen)</summary>
    public async Task<IEnumerable<OperatorListItemDto>> GetBranchOperatorsAsync(Guid branchId)
    {
        return await _db.Operators
            .Where(o => o.BranchId == branchId && o.Status != OperatorStatus.Disabled)
            .OrderBy(o => o.FullName)
            .Select(o => new OperatorListItemDto
            {
                Id = o.Id,
                FullName = o.FullName,
                Username = o.Username,
                Status = o.Status.ToString().ToLowerInvariant(),
            })
            .ToListAsync();
    }

    /// <summary>Verify if admin password is valid</summary>
    public async Task<bool> VerifyAdminPasswordAsync(string password)
    {
        var admin = await _db.Users.FirstOrDefaultAsync(u => u.Role == Roles.SuperAdmin || u.Email == "admin@appleesports.com");
        if (admin == null) return false;
        return BCryptNet.Verify(password, admin.PasswordHash);
    }

    /// <summary>
    /// Generate a 30-day emergency offline JWT.
    /// The token is signed with the same access secret so the existing JWT middleware validates it.
    /// A "token_type" claim of "emergency_offline" allows the client to distinguish it from regular tokens.
    /// </summary>
    public async Task<string> GenerateEmergencyTokenAsync(Guid userId, string role, string? branchId, string? dashboardPermissions)
    {
        var claims = new Dictionary<string, string>
        {
            [ClaimTypes.NameIdentifier] = userId.ToString(),
            [ClaimTypes.Role] = role,
            ["token_type"] = "emergency_offline",
        };

        if (!string.IsNullOrEmpty(branchId))
            claims["branchId"] = branchId;

        if (!string.IsNullOrEmpty(dashboardPermissions))
            claims["dashboardPermissions"] = dashboardPermissions;

        // Resolve the user's display name for the audit log
        string userName = "Unknown";
        if (role == Roles.Operator)
        {
            var op = await _db.Operators.FindAsync(userId);
            if (op != null)
            {
                claims[ClaimTypes.Name] = op.FullName;
                userName = op.FullName;
            }
        }
        else
        {
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                claims[ClaimTypes.Name] = user.FullName;
                userName = user.FullName;
            }
        }

        await _audit.LogAsync(new AuditEntry
        {
            UserId = role != Roles.Operator ? userId : null,
            OperatorId = role == Roles.Operator ? userId : null,
            UserRole = role,
            UserName = userName,
            Action = "emergency_token_generated",
            BranchId = !string.IsNullOrEmpty(branchId) && Guid.TryParse(branchId, out var bid) ? bid : null,
            Details = new { tokenType = "emergency_offline", expiryHours = 720 },
        });

        return _jwt.GenerateEmergencyToken(claims);
    }

    public async Task<CheckSetupResponseDto> CheckSetupStatusAsync()
    {
        var hasSuperAdmin = await _db.Users.AnyAsync(u => u.Role == Roles.SuperAdmin);
        var hasAdmin = await _db.Users.AnyAsync(u => u.Role == Roles.Admin);
        var hasOperator = await _db.Operators.AnyAsync();

        return new CheckSetupResponseDto
        {
            NeedsSuperAdminSetup = !hasSuperAdmin,
            NeedsAdminSetup = !hasAdmin,
            NeedsOperatorSetup = !hasOperator
        };
    }

    public async Task<LoginResponseDto> SetupMasterAccountAsync(SetupMasterDto dto)
    {
        var hasSuperAdmin = await _db.Users.AnyAsync(u => u.Role == Roles.SuperAdmin);
        if (hasSuperAdmin) throw new AuthorizationException("Master account already exists.", "SETUP_LOCKED");

        var adminHash = BCryptNet.HashPassword(dto.Password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            FullName = dto.FullName,
            Role = Roles.SuperAdmin,
            Status = UserStatus.Active,
            PasswordHash = adminHash,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return await LoginAdminAsync(new AdminLoginDto { Email = dto.Email, Password = dto.Password });
    }

    public async Task<LoginResponseDto> SetupOperatorAccountAsync(SetupOperatorDto dto)
    {
        var hasOperator = await _db.Operators.AnyAsync();
        if (hasOperator) throw new AuthorizationException("An operator already exists. Use the dashboard to create more.", "SETUP_LOCKED");

        var opHash = BCryptNet.HashPassword(dto.Password);
        var op = new Operator
        {
            Id = Guid.NewGuid(),
            FullName = dto.FullName,
            Username = dto.Username.Trim().ToLowerInvariant(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            PasswordHash = opHash,
            BranchId = dto.BranchId,
            DashboardPermissions = "{}",
            Status = OperatorStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Operators.Add(op);
        await _db.SaveChangesAsync();

        return await LoginOperatorAsync(new OperatorLoginDto { BranchId = dto.BranchId, Username = dto.Username, Password = dto.Password });
    }

    public async Task InitiatePasswordResetAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        var op = await _db.Operators.FirstOrDefaultAsync(o => o.Email == email);
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == email);

        if (user == null && op == null && member == null) return; // Silent fail for security

        var token = Guid.NewGuid().ToString("N");
        var expiry = DateTimeOffset.UtcNow.AddHours(1);

        string targetName = "";
        if (user != null)
        {
            user.ResetToken = token;
            user.ResetTokenExpiry = expiry;
            targetName = user.FullName;
        }
        else if (op != null)
        {
            op.ResetToken = token;
            op.ResetTokenExpiry = expiry;
            targetName = op.FullName;
        }
        else if (member != null)
        {
            member.ResetToken = token;
            member.ResetTokenExpiry = expiry;
            targetName = member.FullName;
        }

        await _db.SaveChangesAsync();

        string resetLink = $"http://localhost:5173/reset-password?email={email}&token={token}";
        string subject = "Apple Esports - Password Reset";
        string htmlBody = $@"
        <div style='background-color:#050505; color:#ffffff; font-family:""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; padding:40px 20px; text-align:center;'>
            <div style='max-width: 600px; margin: 0 auto; background-color: #111111; border: 1px solid #333333; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.5);'>
                <div style='background: linear-gradient(135deg, #1a1a24 0%, #0d0d14 100%); padding: 30px 20px; border-bottom: 2px solid #dc2626;'>
                    <h1 style='margin: 0; font-size: 28px; letter-spacing: 2px; color: #ffffff; text-transform: uppercase;'>
                        <img src='https://appleesports.in/apple-touch-icon.png' alt='Logo' style='height: 40px; vertical-align: middle; margin-right: 15px;' /> APPLE ESPORTS
                    </h1>
                </div>
                <div style='padding: 40px 30px; text-align: left;'>
                    <h2 style='margin-top: 0; color: #f87171; font-size: 24px; border-bottom: 2px solid #333333; padding-bottom: 15px;'>Password Reset Request</h2>
                    <p style='font-size: 16px; color: #d1d5db; line-height: 1.6;'>Hi <strong>{targetName}</strong>,</p>
                    <p style='font-size: 16px; color: #d1d5db; line-height: 1.6;'>We received a request to reset the password for your Apple Esports account. Click the secure link below to choose a new password:</p>
                    <div style='text-align:center; margin: 40px 0;'>
                        <a href='{resetLink}' style='background: linear-gradient(to right, #dc2626, #ef4444); color: #ffffff; padding: 16px 32px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; letter-spacing: 1px; display: inline-block; box-shadow: 0 4px 15px rgba(220, 38, 38, 0.4);'>RESET MY PASSWORD</a>
                    </div>
                    <p style='color: #6b7280; font-size: 13px; margin-top: 30px;'>If you did not request this password reset, please ignore this email. This link will expire in exactly 1 hour for your security.</p>
                </div>
                <div style='background-color: #080808; padding: 20px; border-top: 1px solid #222222; text-align: center;'>
                    <p style='margin: 0; color: #6b7280; font-size: 12px;'>This is an automated security notification from Apple Esports ERP.</p>
                    <p style='margin: 5px 0 0 0; color: #4b5563; font-size: 11px;'>© {DateTime.UtcNow.Year} Apple Esports. All rights reserved.</p>
                </div>
            </div>
        </div>";

        string targetEmail = email;
        if (email == "admin@appleesports.com")
        {
            targetEmail = "harshalparekh40@gmail.com";
        }

        await _emailService.SendEmailAsync(targetEmail, subject, htmlBody);
    }

    public async Task CompletePasswordResetAsync(ResetPasswordDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.ResetToken == dto.Token && u.ResetTokenExpiry > DateTimeOffset.UtcNow);
        var op = await _db.Operators.FirstOrDefaultAsync(o => o.Email == email && o.ResetToken == dto.Token && o.ResetTokenExpiry > DateTimeOffset.UtcNow);
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == email && m.ResetToken == dto.Token && m.ResetTokenExpiry > DateTimeOffset.UtcNow);

        if (user == null && op == null && member == null) throw new AuthorizationException("Invalid or expired reset token.");

        var newHash = BCryptNet.HashPassword(dto.NewPassword);
        if (user != null)
        {
            user.PasswordHash = newHash;
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
        }
        else if (op != null)
        {
            op.PasswordHash = newHash;
            op.ResetToken = null;
            op.ResetTokenExpiry = null;
        }
        else if (member != null)
        {
            member.PasswordHash = newHash;
            member.ResetToken = null;
            member.ResetTokenExpiry = null;
        }
        await _db.SaveChangesAsync();
    }

    public async Task ChangeCredentialsAsync(Guid userId, string role, ChangeCredentialsDto dto)
    {
        if (role == Roles.SuperAdmin || role == Roles.Admin)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null || !BCryptNet.Verify(dto.CurrentPassword, user.PasswordHash))
                throw new AuthorizationException("Invalid current password.");

            if (!string.IsNullOrEmpty(dto.NewEmail)) user.Email = dto.NewEmail.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(dto.NewPassword)) user.PasswordHash = BCryptNet.HashPassword(dto.NewPassword);
        }
        else if (role == Roles.Operator)
        {
            var op = await _db.Operators.FindAsync(userId);
            if (op == null || !BCryptNet.Verify(dto.CurrentPassword, op.PasswordHash))
                throw new AuthorizationException("Invalid current password.");

            if (!string.IsNullOrEmpty(dto.NewEmail)) op.Email = dto.NewEmail.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(dto.NewUsername)) op.Username = dto.NewUsername.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(dto.NewPassword)) op.PasswordHash = BCryptNet.HashPassword(dto.NewPassword);
        }
        await _db.SaveChangesAsync();
    }
}
