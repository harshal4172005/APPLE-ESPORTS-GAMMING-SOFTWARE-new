namespace AppleEsportsErp.Domain.Enums;

/// <summary>SOP §7.1: PC States — Green=Active, Purple=Reserved, Orange=Awaiting, Gray=Idle, Red=Offline</summary>
public enum PcState
{
    Idle,
    Active,
    Reserved,
    AwaitingBilling,
    Offline,
    UnderMaintenance,
    CloudMode        // Agent connected via internet, Operator PC unreachable
}
