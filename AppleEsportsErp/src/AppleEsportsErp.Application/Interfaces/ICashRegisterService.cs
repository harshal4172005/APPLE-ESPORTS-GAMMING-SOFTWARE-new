using AppleEsportsErp.Application.DTOs.Cash;

namespace AppleEsportsErp.Application.Interfaces;

public interface ICashRegisterService
{
    Task<CashRegisterDto> GetActiveRegisterAsync(Guid branchId, Guid shiftId);

    /// <summary>
    /// Which of the two opening questions to ask, and a reference figure for the second one.
    ///
    /// A branch has one drawer and it runs through the trading day. Only the very first register
    /// this branch ever has, or one opening fresh after a "last shift of the day" close, has
    /// nothing to check a count against. Every other opening is a real count, checked against
    /// what the last shift actually left - see OpenRegisterAsync for what happens when it does
    /// not match.
    /// </summary>
    Task<RegisterOpeningDto> GetOpeningAsync(Guid branchId);

    /// <summary>
    /// Opens the drawer, or refuses to and says why - see OpenRegisterResultDto. The operator's
    /// own count is always what the register opens with; it is never silently replaced by what
    /// was expected.
    /// </summary>
    Task<OpenRegisterResultDto> OpenRegisterAsync(Guid branchId, Guid operatorId, Guid shiftId, OpenRegisterDto dto);
    Task<CashRegisterDto> AddTransactionAsync(Guid branchId, Guid operatorId, Guid shiftId, AddCashTransactionDto dto);
}
