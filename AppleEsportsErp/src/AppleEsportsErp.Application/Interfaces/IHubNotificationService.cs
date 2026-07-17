namespace AppleEsportsErp.Application.Interfaces;

public interface IHubNotificationService
{
    Task BroadcastPcStatusChangeAsync(Guid branchId, Guid pcId);
    Task BroadcastSessionUpdateAsync(Guid branchId, Guid sessionId);
    Task BroadcastReservationUpdateAsync(Guid branchId, Guid reservationId);
    Task BroadcastBillingUpdateAsync(Guid branchId, Guid billId);
    Task BroadcastFoodOrderUpdateAsync(Guid branchId, Guid orderId);
    Task BroadcastCashRegisterUpdateAsync(Guid branchId, Guid registerId);
    Task BroadcastPcManagementUpdateAsync(Guid branchId, Guid pcId, string action);
    Task SendUnlockCommandToAgentAsync(Guid pcId, int durationMinutes, string? customerName);
    Task SendLockCommandToAgentAsync(Guid pcId);
    Task TriggerDashboardRefreshAsync();
}
