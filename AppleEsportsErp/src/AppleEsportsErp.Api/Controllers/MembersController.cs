using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Members;
using AppleEsportsErp.Application.Interfaces;
using System.Security.Claims;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/members")]
[Authorize]
[BranchIsolation]
public class MembersController : ControllerBase
{
    private readonly IMemberService _memberService;

    public MembersController(IMemberService memberService)
    {
        _memberService = memberService;
    }

    private Guid GetBranchId() 
    {
        var val = HttpContext.Items["BranchId"]?.ToString();
        return string.IsNullOrEmpty(val) ? Guid.Empty : Guid.Parse(val);
    }

    [HttpGet]
    public async Task<IActionResult> GetMembers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _memberService.GetMembersAsync(GetBranchId(), search, page, pageSize);
        return Ok(ApiResponse<PaginatedResult<MemberDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMemberById(Guid id)
    {
        var result = await _memberService.GetMemberByIdAsync(id);
        return Ok(ApiResponse<MemberDto>.Ok(result));
    }

    [HttpGet("phone/{mobileNumber}")]
    public async Task<IActionResult> GetMemberByMobile(string mobileNumber)
    {
        var result = await _memberService.GetMemberByMobileAsync(mobileNumber);
        return Ok(ApiResponse<MemberDto>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> RegisterMember([FromBody] RegisterMemberDto dto)
    {
        var result = await _memberService.RegisterMemberAsync(GetBranchId(), (await this.GetOperatorIdAsync()), dto);
        return Ok(ApiResponse<MemberDto>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMember(Guid id, [FromBody] UpdateMemberDto dto)
    {
        Console.WriteLine($"[DEBUG UpdateMember] id: {id}, FullName: {dto.FullName}, DisableLogin: {dto.DisableLogin}, Username: {dto.Username}");
        var result = await _memberService.UpdateMemberAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id, dto);
        return Ok(ApiResponse<MemberDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMember(Guid id)
    {
        await _memberService.DeleteMemberAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id);
        return Ok(ApiResponse<object>.Ok(null));
    }

    /// <summary>Member self-login — POST /api/members/login (no auth required)</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> MemberLogin([FromBody] MemberLoginDto dto)
    {
        var result = await _memberService.LoginMemberAsync(dto);
        return Ok(ApiResponse<MemberLoginResponseDto>.Ok(result));
    }
}

