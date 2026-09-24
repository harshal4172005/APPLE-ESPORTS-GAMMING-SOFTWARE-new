namespace AppleEsportsErp.Application.Services;

/// <summary>
/// Single source of truth for time as the business experiences it: Indian Standard Time.
///
/// Instants are still stored in UTC — that is what <see cref="DateTimeOffset"/> and the
/// database's "timestamp with time zone" columns are for, and it is what keeps sync between
/// branches and Head Office unambiguous. IST is applied at the edges: when deciding which
/// business day something belongs to, and when showing a time to a human.
///
/// A fixed +05:30 offset is used rather than <c>TimeZoneInfo.FindSystemTimeZoneById</c>.
/// India has no daylight saving and never has, so the offset is constant — and the lookup
/// id differs by platform ("India Standard Time" on Windows, "Asia/Kolkata" on Linux).
/// The API runs in Linux containers on the server and on Windows at the branches, so a
/// hard-coded offset is both correct and the only thing that behaves identically on both.
/// </summary>
public static class IndiaTime
{
    /// <summary>IST is UTC+05:30, year round.</summary>
    public static readonly TimeSpan Offset = new(5, 30, 0);

    /// <summary>
    /// Hour (IST) at which one trading day ends and the next begins.
    ///
    /// This was 6 (the dead window between a 2am close and a 10am open, so a night's
    /// trading was never split across two EOD reports). Changed to plain midnight on
    /// 2026-08-23 by explicit business decision - a night that runs past midnight now
    /// splits across two calendar-day reports, which is accepted as the tradeoff for a
    /// boundary that matches the date everyone already reads off a clock or a receipt.
    /// </summary>
    public const int BusinessDayStartHour = 0;

    /// <summary>Current moment, expressed in IST.</summary>
    public static DateTimeOffset Now => DateTimeOffset.UtcNow.ToOffset(Offset);

    /// <summary>Re-express any instant in IST. The moment itself does not change.</summary>
    public static DateTimeOffset ToIst(DateTimeOffset instant) => instant.ToOffset(Offset);

    /// <summary>
    /// The trading day an instant belongs to. A bill rung up at 01:30 IST counts towards
    /// the night that began the previous morning, which is how the operator on shift
    /// thinks about it.
    /// </summary>
    public static DateOnly BusinessDayOf(DateTimeOffset instant)
    {
        var ist = ToIst(instant);
        return DateOnly.FromDateTime(
            ist.Hour < BusinessDayStartHour
                ? ist.Date.AddDays(-1)
                : ist.Date);
    }

    /// <summary>
    /// Half-open UTC range [start, end) covering one trading day, ready to hand to a
    /// database query.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End) BusinessDayRange(DateOnly businessDay)
    {
        var start = new DateTimeOffset(
            businessDay.Year, businessDay.Month, businessDay.Day,
            BusinessDayStartHour, 0, 0, Offset);

        return (start.ToUniversalTime(), start.AddDays(1).ToUniversalTime());
    }

    /// <summary>Trading day range for whichever day the given instant falls in.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) BusinessDayRangeFor(DateTimeOffset instant) =>
        BusinessDayRange(BusinessDayOf(instant));

    /// <summary>Formats an instant in IST for display or printing, e.g. "08 Aug 2026, 01:30 AM".</summary>
    public static string Format(DateTimeOffset instant) =>
        ToIst(instant).ToString("dd MMM yyyy, hh:mm tt");

    /// <summary>Formats just the clock time in IST, e.g. "01:30 AM".</summary>
    public static string FormatTime(DateTimeOffset instant) =>
        ToIst(instant).ToString("hh:mm tt");
}
