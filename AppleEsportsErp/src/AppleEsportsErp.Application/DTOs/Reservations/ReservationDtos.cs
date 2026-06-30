using System.ComponentModel.DataAnnotations;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Application.DTOs.Reservations;

public class ReservationDto
{
    public Guid Id { get; set; }
    public Guid PcId { get; set; }
    public string CustomerName { get; set; } = null!;
    public Guid? MemberId { get; set; }
    public DateTimeOffset ReservationTime { get; set; }
    public int? DurationMin { get; set; }
    public ReservationState State { get; set; }
    public string? Notes { get; set; }
    public decimal AdvanceDeposit { get; set; }
    public int GracePeriodMin { get; set; }
    public string? PcName { get; set; }
}

public class CreateReservationDto
{
    [Required]
    public Guid PcId { get; set; }
    [Required]
    public string CustomerName { get; set; } = null!;
    public Guid? MemberId { get; set; }
    [Required]
    public DateTimeOffset ReservationTime { get; set; }
    public int? DurationMin { get; set; }
    public string? Notes { get; set; }
    public decimal AdvanceDeposit { get; set; }
    public int? GracePeriodMin { get; set; }
}

public class CancelReservationDto
{
    public string? Reason { get; set; }
}

public class OverrideReservationDto
{
    [Required]
    public string Reason { get; set; } = null!;
}
