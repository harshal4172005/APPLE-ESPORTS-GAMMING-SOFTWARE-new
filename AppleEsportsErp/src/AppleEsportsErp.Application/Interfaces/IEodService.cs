using AppleEsportsErp.Application.DTOs.Eod;

namespace AppleEsportsErp.Application.Interfaces;

public interface IEodService
{
    /// <summary>
    /// businessDay is the IST trading day itself (2026-09-01), never a DateTimeOffset - a
    /// DateTimeOffset built from a plain "YYYY-MM-DD" string round-trips through whatever
    /// timezone the CURRENT PROCESS happens to be running in (India Standard Time on a
    /// branch's own Windows machine, UTC in Head Office's Linux container), silently shifting
    /// which calendar day gets reported depending on where the request lands. DateOnly has no
    /// offset to get wrong.
    /// </summary>
    Task<EodReportDto> GenerateEodReportAsync(Guid branchId, DateOnly businessDay);
}
