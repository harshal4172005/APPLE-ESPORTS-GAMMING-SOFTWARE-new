using System;

namespace AppleEsportsErp.Domain.Entities;

public class CustomerCredit
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; }

    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; }

    public Guid BillId { get; set; }
    public Bill Bill { get; set; }

    public string CustomerName { get; set; }
    public string CustomerPhone { get; set; }
    public string PcNumber { get; set; }

    public decimal OriginalBillAmount { get; set; }
    public decimal AmountPaidInitially { get; set; }
    public decimal CreditAmount { get; set; }

    public string Status { get; set; } // "pending" or "cleared"

    public DateTimeOffset? ClearedAt { get; set; }
    
    // Track who cleared it
    public Guid? ClearedByOperatorId { get; set; }
    public Operator ClearedByOperator { get; set; }
}
