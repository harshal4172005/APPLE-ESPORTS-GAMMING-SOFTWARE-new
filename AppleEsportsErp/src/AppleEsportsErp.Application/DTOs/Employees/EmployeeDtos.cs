namespace AppleEsportsErp.Application.DTOs.Employees;

public class EmployeeDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public string EmployeeNumber { get; set; } = "";

    // Personal
    public string FullName { get; set; } = "";
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Nationality { get; set; }
    public string? MaritalStatus { get; set; }

    // Contact
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

    // Job
    public string? PositionTitle { get; set; }
    public string? Department { get; set; }
    public string? Supervisor { get; set; }
    public DateOnly? StartDate { get; set; }

    // Banking
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
    public string? BankBranch { get; set; }

    // Reference
    public string? RefName { get; set; }
    public string? RefRelationship { get; set; }
    public string? RefPhone { get; set; }
    public string? RefAddress { get; set; }

    public string Status { get; set; } = "Active";
    public string? SubmittedByName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateEmployeeDto
{
    // Personal
    public string FullName { get; set; } = "";
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Nationality { get; set; }
    public string? MaritalStatus { get; set; }

    // Contact
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

    // Job
    public string? PositionTitle { get; set; }
    public string? Department { get; set; }
    public string? Supervisor { get; set; }
    public DateOnly? StartDate { get; set; }

    // Banking
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
    public string? BankBranch { get; set; }

    // Reference
    public string? RefName { get; set; }
    public string? RefRelationship { get; set; }
    public string? RefPhone { get; set; }
    public string? RefAddress { get; set; }

    // System Account Link
    public bool CreateSystemAccount { get; set; }
    public string? SystemRole { get; set; } // "Operator" or "Admin"
    public string? SystemUsername { get; set; }
    public string? SystemPassword { get; set; }
    public string? SystemPin { get; set; }
}
