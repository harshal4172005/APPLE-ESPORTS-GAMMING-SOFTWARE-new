using AppleEsportsErp.Application.DTOs;

namespace AppleEsportsErp.Application.Interfaces;

public interface IVersionService
{
    Task<VersionInfoDto?> GetLatestVersionAsync();
    Task<List<VersionInfoDto>> GetVersionHistoryAsync();
    Task<List<BranchVersionStatusDto>> GetAllBranchVersionStatusesAsync();
    Task<BranchVersionStatusDto?> GetBranchVersionStatusAsync(Guid branchId);
    Task<VersionInfoDto> CreateVersionAsync(string version, string releaseNotes);
    Task<VersionInfoDto> ApproveVersionAsync(int versionInfoId, string userId);
    Task<VersionInfoDto> UnapproveVersionAsync(int versionInfoId);
    Task<(string? InstallerFileName, string? AgentFileName)> DeleteVersionAsync(int versionInfoId);
    Task UpdateBranchAutoUpdateAsync(Guid branchId, bool autoUpdateEnabled);
    Task UpdateBranchVersionStatusAsync(Guid branchId, string currentVersion, int upToDateCount, int totalCount);
    Task ReportUpdateProgressAsync(Guid branchId, string stage, int progressPercent, string? message);
}
