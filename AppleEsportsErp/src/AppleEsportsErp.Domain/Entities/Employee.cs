namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §HR-01: Employee HR records linked to branches</summary>
public class Employee
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string EmployeeNumber { get; set; } = null!;

    // Personal Information
    public string FullName { get; set; } = null!;
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Nationality { get; set; } = "Indian";
    public string? MaritalStatus { get; set; }

    // Contact Information
    public string? PermanentAddress { get; set; }
    public string? CurrentAddress { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    // Emergency Contact
    public string? EmergencyName { get; set; }
    public string? EmergencyRelationship { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? EmergencyEmail { get; set; }
    public string? EmergencyAddress { get; set; }

    // Job Information
    public string? PositionTitle { get; set; }
    public string? Department { get; set; }
    public string? Supervisor { get; set; }
    public DateOnly? StartDate { get; set; }

    // Banking Information
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
    public string? BankBranch { get; set; }

    // Reference
    public string? RefName { get; set; }
    public string? RefRelationship { get; set; }
    public string? RefPhone { get; set; }
    public string? RefAddress { get; set; }

    // System Fields
    public string Status { get; set; } = "Active";
    public Guid? SubmittedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Branch? Branch { get; set; }
    public Operator? SubmittedByOperator { get; set; }
}
