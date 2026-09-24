using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Reservations;

namespace AppleEsportsErp.Application.Interfaces;

public interface IReservationService
{
    Task<PaginatedResult<ReservationDto>> GetActiveReservationsAsync(Guid branchId, int page = 1, int pageSize = 50);
    Task<List<ReservationDto>> GetReservationHistoryAsync(Guid branchId, DateOnly fromDate, DateOnly toDate);
    Task<ReservationDto> GetReservationAsync(Guid id);
    Task<ReservationDto> CreateReservationAsync(Guid branchId, Guid operatorId, CreateReservationDto dto);
    Task<ReservationDto> CancelReservationAsync(Guid branchId, Guid operatorId, Guid id, CancelReservationDto dto);
    Task<ReservationDto> StartReservedSessionAsync(Guid branchId, Guid operatorId, Guid id);
    Task<ReservationDto> OverrideReservationAsync(Guid branchId, Guid operatorId, Guid id, OverrideReservationDto dto);

    /// <summary>A plain "the customer is here" reminder flag - see ReservationService.SetArrivedAsync.</summary>
    Task<ReservationDto> SetArrivedAsync(Guid branchId, Guid id, bool arrived);

    /// <summary>Permanently removes a reservation - unlike Cancel, no reason kept, no record left.</summary>
    Task DeleteReservationAsync(Guid branchId, Guid operatorId, Guid id);

    // Auto-expire reservations (can be called periodically or passively on fetch)
    Task ExpirePastReservationsAsync(Guid branchId);
}
