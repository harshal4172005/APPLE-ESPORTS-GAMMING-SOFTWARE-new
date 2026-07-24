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
    Task<MemberDto> AdminEditValuesAsync(Guid branchId, Guid adminId, Guid id, AdminEditMemberValuesDto dto);
}
