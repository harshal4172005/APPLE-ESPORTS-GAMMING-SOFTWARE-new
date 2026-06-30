namespace AppleEsportsErp.Domain.Enums;

/// <summary>SOP §12.1: Food Order States</summary>
public enum OrderStatus
{
    Pending,
    Preparing,
    Ready,
    Delivered,
    Completed,
    Cancelled
}
