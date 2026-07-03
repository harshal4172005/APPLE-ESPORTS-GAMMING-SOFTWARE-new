using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using BCryptNet = BCrypt.Net.BCrypt;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Members;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Identity;

namespace AppleEsportsErp.Infrastructure.Services;

public class MemberService : IMemberService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly JwtTokenService _jwt;
    private readonly IEmailService _emailService;

    public MemberService(IUnitOfWork unitOfWork, IAuditService auditService, JwtTokenService jwt, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _jwt = jwt;
        _emailService = emailService;
    }

    public async Task<PaginatedResult<MemberDto>> GetMembersAsync(Guid branchId, string? search, int page = 1, int pageSize = 50)
    {
        var query = _unitOfWork.Repository<Member>().Query()
            .Where(m => m.Status != MemberStatus.Suspended);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(m => 
                m.MobileNumber.Contains(s) || 
                m.FullName.ToLower().Contains(s) || 
                m.MemberNumber.ToLower().Contains(s) ||
                (m.Username != null && m.Username.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(m => m.JoinDate)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();

        var dtos = items.Select(MapToDto).ToList();
        return new PaginatedResult<MemberDto>(dtos, total, page, pageSize);
    }

    public async Task<MemberDto> GetMemberByIdAsync(Guid id)
    {
        var member = await _unitOfWork.Repository<Member>().GetByIdAsync(id)
            ?? throw new NotFoundException("Member not found.");

        return MapToDto(member);
    }

    public async Task<MemberDto> GetMemberByMobileAsync(string mobileNumber)
    {
        var member = await _unitOfWork.Repository<Member>().Query()
            .FirstOrDefaultAsync(m => m.MobileNumber == mobileNumber)
            ?? throw new NotFoundException($"Member with mobile {mobileNumber} not found.");

        return MapToDto(member);
    }

    public async Task<MemberDto> RegisterMemberAsync(Guid branchId, Guid operatorId, RegisterMemberDto dto)
    {
        var exists = await _unitOfWork.Repository<Member>().Query()
            .AnyAsync(m => m.MobileNumber == dto.MobileNumber);

        if (exists)
            throw new AppException("A member with this mobile number already exists.");

        // Username uniqueness check
        if (!string.IsNullOrWhiteSpace(dto.Username))
        {
            var usernameTaken = await _unitOfWork.Repository<Member>().Query()
                .AnyAsync(m => m.Username == dto.Username.Trim().ToLowerInvariant());
            if (usernameTaken)
                throw new AppException($"Username '{dto.Username}' is already taken.");
        }

        // Require password when username is set
        if (!string.IsNullOrWhiteSpace(dto.Username) && string.IsNullOrWhiteSpace(dto.Password))
            throw new AppException("A password is required when setting a username.");

        // Generate member number: MEM-YYMM-XXXX
        var count = await _unitOfWork.Repository<Member>().Query().CountAsync() + 1;
        var memberNum = $"MEM-{DateTime.UtcNow:yyMM}-{count:D4}";

        var member = new Member
        {
            MemberNumber = memberNum,
            FullName = dto.FullName,
            MobileNumber = dto.MobileNumber,
            Email = dto.Email,
            Username = string.IsNullOrWhiteSpace(dto.Username) ? null : dto.Username.Trim().ToLowerInvariant(),
            PasswordHash = string.IsNullOrWhiteSpace(dto.Password) ? null : BCryptNet.HashPassword(dto.Password),
            Status = MemberStatus.Active,
            HomeBranchId = branchId,
            JoinDate = DateTimeOffset.UtcNow,
            CreatedBy = operatorId,
            GamingBalance = 0,
            FoodBalance = 0,
            GamingPoints = 0,
            FoodPoints = 0,
            TotalPoints = 0
        };

        await _unitOfWork.Repository<Member>().AddAsync(member);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = AuditActions.MemberCreate,
            BranchId = branchId,
            TargetType = "member",
            TargetId = member.Id,
            Details = new { MemberNumber = member.MemberNumber, FullName = dto.FullName }
        });

        await _unitOfWork.CommitTransactionAsync();

        // Send Email Notification
        var branchName = "Unknown Branch";
        var branch = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Query()
            .FirstOrDefaultAsync(b => b.Id == branchId);
        if (branch != null) branchName = branch.Name;

        string emailBody = $@"
        <div style='background-color:#050505; color:#ffffff; font-family:""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; padding:40px 20px; text-align:center;'>
            <div style='max-width: 600px; margin: 0 auto; background-color: #111111; border: 1px solid #333333; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.5);'>
                <div style='background: linear-gradient(135deg, #1a1a24 0%, #0d0d14 100%); padding: 30px 20px; border-bottom: 2px solid #dc2626;'>
                    <h1 style='margin: 0; font-size: 28px; letter-spacing: 2px; color: #ffffff; text-transform: uppercase;'>
                        <img src='https://appleesports.in/apple-touch-icon.png' alt='Logo' style='height: 40px; vertical-align: middle; margin-right: 15px;' /> APPLE ESPORTS
                    </h1>
                </div>
                <div style='padding: 40px 30px; text-align: left;'>
                    <h2 style='margin-top: 0; color: #3b82f6; font-size: 24px; border-bottom: 2px solid #333333; padding-bottom: 15px;'>New Member Joined</h2>
                    <p style='font-size: 16px; color: #d1d5db; line-height: 1.6;'>A new member has officially joined the Apple Esports system.</p>
                    
                    <div style='background-color: #0a0a0a; border: 1px solid #222222; border-radius: 8px; padding: 20px; margin-top: 25px;'>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Name:</span> <strong style='color: #ffffff;'>{member.FullName}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Mobile:</span> <strong style='color: #ffffff;'>{member.MobileNumber}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Member ID:</span> <strong style='color: #ffffff;'>{member.MemberNumber}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Branch:</span> <strong style='color: #ffffff;'>{branchName}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Time:</span> <strong style='color: #ffffff;'>{member.JoinDate.ToString("MMM dd, yyyy HH:mm")}</strong></p>
                    </div>
                </div>
                <div style='background-color: #080808; padding: 20px; border-top: 1px solid #222222; text-align: center;'>
                    <p style='margin: 0; color: #6b7280; font-size: 12px;'>This is an automated notification from Apple Esports ERP.</p>
                    <p style='margin: 5px 0 0 0; color: #4b5563; font-size: 11px;'>© {DateTime.UtcNow.Year} Apple Esports. All rights reserved.</p>
                </div>
            </div>
        </div>";

        await SendNotificationAsync($"New Member Joined: {member.FullName} (ID: {member.MemberNumber})", emailBody);

        return MapToDto(member);
    }

    public async Task<MemberDto> UpdateMemberAsync(Guid branchId, Guid operatorId, Guid id, UpdateMemberDto dto)
    {
        var member = await _unitOfWork.Repository<Member>().GetByIdAsync(id)
            ?? throw new NotFoundException("Member not found.");

        // Check if new mobile belongs to someone else
        var duplicate = await _unitOfWork.Repository<Member>().Query()
            .AnyAsync(m => m.MobileNumber == dto.MobileNumber && m.Id != id);

        if (duplicate)
            throw new AppException("The specified mobile number is already in use by another member.");

        member.FullName = dto.FullName;
        member.MobileNumber = dto.MobileNumber;
        member.Email = dto.Email;
        member.UpdatedAt = DateTimeOffset.UtcNow;

        // Update username if provided
        if (dto.DisableLogin == true)
        {
            member.Username = null;
            member.PasswordHash = null;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(dto.Username))
            {
                var newUsername = dto.Username.Trim().ToLowerInvariant();
                var usernameTaken = await _unitOfWork.Repository<Member>().Query()
                    .AnyAsync(m => m.Username == newUsername && m.Id != id);
                if (usernameTaken)
                    throw new AppException($"Username '{dto.Username}' is already taken.");
                member.Username = newUsername;
            }

            // Update password if provided
            if (!string.IsNullOrWhiteSpace(dto.Password))
                member.PasswordHash = BCryptNet.HashPassword(dto.Password);

            // Safety check: require password hash if username is assigned
            if (member.Username != null && string.IsNullOrWhiteSpace(member.PasswordHash))
                throw new AppException("A password is required when setting a username.");
        }

        _unitOfWork.Repository<Member>().Update(member);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = "member_update",
            BranchId = branchId,
            TargetType = "member",
            TargetId = member.Id,
            Details = new { MemberNumber = member.MemberNumber }
        });

        await _unitOfWork.CommitTransactionAsync();

        return MapToDto(member);
    }

    public async Task DeleteMemberAsync(Guid branchId, Guid operatorId, Guid id)
    {
        var member = await _unitOfWork.Repository<Member>().GetByIdAsync(id)
            ?? throw new NotFoundException("Member not found.");

        // Soft delete: set status to Suspended
        member.Status = MemberStatus.Suspended;
        member.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<Member>().Update(member);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = "member_delete",
            BranchId = branchId,
            TargetType = "member",
            TargetId = member.Id,
            Details = new { MemberNumber = member.MemberNumber, FullName = member.FullName }
        });

        await _unitOfWork.CommitTransactionAsync();

        // Send Email Notification
        string emailBody = $@"
        <div style='background-color:#050505; color:#ffffff; font-family:""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; padding:40px 20px; text-align:center;'>
            <div style='max-width: 600px; margin: 0 auto; background-color: #111111; border: 1px solid #333333; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.5);'>
                <div style='background: linear-gradient(135deg, #1a1a24 0%, #0d0d14 100%); padding: 30px 20px; border-bottom: 2px solid #dc2626;'>
                    <h1 style='margin: 0; font-size: 28px; letter-spacing: 2px; color: #ffffff; text-transform: uppercase;'>
                        <img src='https://appleesports.in/apple-touch-icon.png' alt='Logo' style='height: 40px; vertical-align: middle; margin-right: 15px;' /> APPLE ESPORTS
                    </h1>
                </div>
                <div style='padding: 40px 30px; text-align: left;'>
                    <h2 style='margin-top: 0; color: #ef4444; font-size: 24px; border-bottom: 2px solid #333333; padding-bottom: 15px;'>Member Deleted</h2>
                    <p style='font-size: 16px; color: #d1d5db; line-height: 1.6;'>A member account has been removed from the system.</p>
                    
                    <div style='background-color: #0a0a0a; border: 1px solid #222222; border-radius: 8px; padding: 20px; margin-top: 25px;'>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Name:</span> <strong style='color: #ffffff;'>{member.FullName}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Mobile:</span> <strong style='color: #ffffff;'>{member.MobileNumber}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Member ID:</span> <strong style='color: #ffffff;'>{member.MemberNumber}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Time:</span> <strong style='color: #ffffff;'>{DateTimeOffset.UtcNow.ToString("MMM dd, yyyy HH:mm")}</strong></p>
                    </div>
                </div>
                <div style='background-color: #080808; padding: 20px; border-top: 1px solid #222222; text-align: center;'>
                    <p style='margin: 0; color: #6b7280; font-size: 12px;'>This is an automated security notification from Apple Esports ERP.</p>
                    <p style='margin: 5px 0 0 0; color: #4b5563; font-size: 11px;'>© {DateTime.UtcNow.Year} Apple Esports. All rights reserved.</p>
                </div>
            </div>
        </div>";

        await SendNotificationAsync($"Member Suspended/Deleted: {member.FullName} (ID: {member.MemberNumber})", emailBody);
    }

    private async Task SendNotificationAsync(string subject, string body)
    {
        try 
        {
            var config = await _unitOfWork.Repository<SystemConfig>().Query()
                .FirstOrDefaultAsync(c => c.ConfigKey == "global_system_rules");
            if (config != null && !string.IsNullOrEmpty(config.ConfigValue))
            {
                var doc = System.Text.Json.JsonDocument.Parse(config.ConfigValue);
                if (doc.RootElement.TryGetProperty("emailNotifications", out var emailNode))
                {
                    if (emailNode.TryGetProperty("receivers", out var receiversNode))
                    {
                        var receivers = receiversNode.GetString();
                        if (!string.IsNullOrWhiteSpace(receivers))
                        {
                            await _emailService.SendEmailAsync(receivers, subject, body);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MemberService] Failed to send email notification: {ex.Message}");
        }
    }

    public async Task<MemberLoginResponseDto> LoginMemberAsync(MemberLoginDto dto)
    {
        var identifier = dto.Identifier.Trim().ToLowerInvariant();
        var member = await _unitOfWork.Repository<Member>().Query()
            .FirstOrDefaultAsync(m => (m.Username != null && m.Username.ToLower() == identifier) || 
                                      (m.MobileNumber != null && m.MobileNumber == identifier) || 
                                      (m.Email != null && m.Email.ToLower() == identifier));

        if (member == null || string.IsNullOrEmpty(member.PasswordHash))
            throw new AuthenticationException("Invalid username or password.", "INVALID_CREDENTIALS");

        if (member.Status != MemberStatus.Active)
            throw new AuthorizationException("Member account is inactive.", "ACCOUNT_INACTIVE");

        if (!BCryptNet.Verify(dto.Password, member.PasswordHash))
            throw new AuthenticationException("Invalid username or password.", "INVALID_CREDENTIALS");

        var claims = new Dictionary<string, string>
        {
            [ClaimTypes.NameIdentifier] = member.Id.ToString(),
            [ClaimTypes.Role] = "Member",
            [ClaimTypes.Name] = member.FullName,
            ["memberNumber"] = member.MemberNumber,
        };

        var token = _jwt.GenerateAccessToken(claims);

        return new MemberLoginResponseDto
        {
            MemberId = member.Id,
            MemberNumber = member.MemberNumber,
            FullName = member.FullName,
            GamingBalance = member.GamingBalance,
            FoodBalance = member.FoodBalance,
            Token = token,
        };
    }

    private static MemberDto MapToDto(Member m)
    {
        return new MemberDto
        {
            Id = m.Id,
            MemberNumber = m.MemberNumber,
            FullName = m.FullName,
            MobileNumber = m.MobileNumber,
            Email = m.Email,
            Username = m.Username,
            HasPassword = !string.IsNullOrEmpty(m.PasswordHash),
            Status = m.Status,
            GamingBalance = m.GamingBalance,
            FoodBalance = m.FoodBalance,
            GamingPoints = m.GamingPoints,
            FoodPoints = m.FoodPoints,
            TotalPoints = m.TotalPoints,
            JoinDate = m.JoinDate,
            LastVisit = m.LastVisit
        };
    }
}
