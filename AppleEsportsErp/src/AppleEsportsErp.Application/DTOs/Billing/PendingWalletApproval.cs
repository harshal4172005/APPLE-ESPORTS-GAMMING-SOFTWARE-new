using System;

namespace AppleEsportsErp.Application.DTOs.Billing;

public class PendingWalletApproval
{
    public Guid BillId { get; set; }
    public Guid OperatorId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid BranchId { get; set; }
    public decimal Amount { get; set; }
}
