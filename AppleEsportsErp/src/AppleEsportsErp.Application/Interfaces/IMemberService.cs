using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Members;

namespace AppleEsportsErp.Application.Interfaces;

public interface IMemberService
{
    Task<PaginatedResult<MemberDto>> GetMembersAsync(Guid branchId, string? search, int page = 1, int pageSize = 50, bool includeDeleted = false);
    Task<MemberDto> GetMemberByIdAsync(Guid id);
    Task<MemberDto> GetMemberByMobileAsync(string mobileNumber);
    Task<MemberDto> RegisterMemberAsync(Guid branchId, Guid operatorId, RegisterMemberDto dto);
    Task<MemberDto> UpdateMemberAsync(Guid branchId, Guid operatorId, Guid id, UpdateMemberDto dto);
    Task DeleteMemberAsync(Guid branchId, Guid operatorId, Guid id);
    Task<MemberLoginResponseDto> LoginMemberAsync(MemberLoginDto dto);
    /// <summary>See MemberService.AdminEditValuesAsync: <paramref name="remoteAdminName"/> is
    /// set only when applying this on behalf of a Head Office admin acting remotely.</summary>
    Task<MemberDto> AdminEditValuesAsync(Guid branchId, Guid adminId, Guid id, AdminEditMemberValuesDto dto, string? remoteAdminName = null);

    /// <summary>Everything that happened for one member - gaming sessions and wallet
    /// top-ups/deductions together, across every branch they've ever played at (a member's
    /// wallet is not branch-locked, so neither is this). Optional date range; open-ended
    /// when either end is omitted.</summary>
    Task<List<MemberHistoryEntryDto>> GetMemberHistoryAsync(Guid memberId, DateOnly? fromDate, DateOnly? toDate);
}
