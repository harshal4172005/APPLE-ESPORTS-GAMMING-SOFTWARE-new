using System.ComponentModel.DataAnnotations;

namespace AppleEsportsErp.Application.DTOs.Cash;

public class DenominationCountDto
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public int Notes2000 { get; set; }
    public int Notes500 { get; set; }
    public int Notes200 { get; set; }
    public int Notes100 { get; set; }
    public int Notes50 { get; set; }
    public int Notes20 { get; set; }
    public int Notes10 { get; set; }
    public int Coins5 { get; set; }
    public int Coins2 { get; set; }
    public int Coins1 { get; set; }
    public decimal CountedTotal { get; set; }
    public decimal ExpectedTotal { get; set; }
    public decimal Difference { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class SubmitDenominationDto
{
    public int Notes2000 { get; set; }
    public int Notes500 { get; set; }
    public int Notes200 { get; set; }
    public int Notes100 { get; set; }
    public int Notes50 { get; set; }
    public int Notes20 { get; set; }
    public int Notes10 { get; set; }
    public int Coins5 { get; set; }
    public int Coins2 { get; set; }
    public int Coins1 { get; set; }
    
    public string? MismatchReason { get; set; }
}
