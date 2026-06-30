namespace AppleEsportsErp.Domain.Enums;

/// <summary>SOP §8: Session lifecycle states</summary>
public enum SessionState
{
    Active,
    Reserved,
    AwaitingBilling,
    Completed,
    Expired
}
