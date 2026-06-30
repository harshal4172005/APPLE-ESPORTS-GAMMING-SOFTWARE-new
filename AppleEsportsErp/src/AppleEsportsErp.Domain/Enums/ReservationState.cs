namespace AppleEsportsErp.Domain.Enums;

/// <summary>SOP §8: Reservation system states</summary>
public enum ReservationState
{
    Pending,
    Active,
    Completed,
    Expired,
    Cancelled,
    Overridden
}
