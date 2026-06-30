using AppleEsportsErp.Application.DTOs.Eod;

namespace AppleEsportsErp.Application.Interfaces;

public interface IEodService
{
    Task<EodReportDto> GenerateEodReportAsync(Guid branchId, DateTimeOffset targetDate);
    Task<ValidationStatusDto> GetValidationStatusAsync(Guid branchId, DateTimeOffset targetDate);
    Task<EodSnapshotDto> FinalizeEodAsync(Guid branchId, Guid operatorId, DateTimeOffset targetDate);
    Task<EodSnapshotDto?> GetHistoricalEodAsync(Guid branchId, DateTimeOffset targetDate);
}
